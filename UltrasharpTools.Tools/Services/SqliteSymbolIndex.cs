using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// SQLite-based symbol index with FTS5 full-text search.
/// Replaces in-memory FastSymbolIndex to reduce memory footprint from ~150MB to ~5MB.
/// All symbol data is stored on disk with FTS5 indexing for fast searches.
/// </summary>
public sealed class SqliteSymbolIndex : IAsyncDisposable {
    private readonly ILogger _logger;
    private readonly string _dbPath;
    private SqliteConnection? _connection;
    private bool _isBuilt;

    // Small LRU cache for hot symbols (FQN -> resolved data)
    private readonly ConcurrentDictionary<string, SymbolIndexEntry> _hotCache = new();
    private const int MaxHotCacheSize = 1000;

    public SqliteSymbolIndex(ILogger logger, string? dbPath = null) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbPath = dbPath ?? GetDefaultDbPath();
    }

    public bool IsBuilt => _isBuilt;
    public int TotalSymbols { get; private set; }

    private static string GetDefaultDbPath() {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "UltrasharpTools", "SymbolIndex");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "symbols.db");
    }

    /// <summary>
    /// Initialize SQLite database with schema.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        var connectionString = new SqliteConnectionStringBuilder {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
        }.ConnectionString;

        _connection = new SqliteConnection(connectionString);
        await _connection.OpenAsync(cancellationToken);

        // Enable WAL mode for better concurrent access
        await ExecuteNonQueryAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await ExecuteNonQueryAsync("PRAGMA synchronous=NORMAL;", cancellationToken);
        await ExecuteNonQueryAsync("PRAGMA cache_size=-64000;", cancellationToken); // 64MB cache
        await ExecuteNonQueryAsync("PRAGMA temp_store=MEMORY;", cancellationToken);

        await CreateSchemaAsync(cancellationToken);

        _logger.LogInformation("SQLite symbol index initialized at {DbPath}", _dbPath);
    }

    private async Task CreateSchemaAsync(CancellationToken cancellationToken) {
        const string schema = """
-- Main symbols table
CREATE TABLE IF NOT EXISTS symbols (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    canonical_fqn TEXT NOT NULL,
    simple_name TEXT NOT NULL,
    namespace TEXT NOT NULL,
    flags INTEGER NOT NULL,
    namespace_depth INTEGER NOT NULL,
    name_length INTEGER NOT NULL,
    fqn_length INTEGER NOT NULL,
    first_char TEXT NOT NULL,
    simple_name_hash INTEGER NOT NULL,
    assembly_name TEXT,
    project_name TEXT,
    file_path TEXT,
    line_number INTEGER DEFAULT 0,
    document_id TEXT,
    solution_hash TEXT
);

-- FTS5 virtual table for full-text search
CREATE VIRTUAL TABLE IF NOT EXISTS symbols_fts USING fts5(
    simple_name,
    canonical_fqn,
    namespace,
    content=symbols,
    content_rowid=id,
    tokenize='unicode61 remove_diacritics 2'
);

-- Triggers to keep FTS in sync
CREATE TRIGGER IF NOT EXISTS symbols_ai AFTER INSERT ON symbols BEGIN
    INSERT INTO symbols_fts(rowid, simple_name, canonical_fqn, namespace)
    VALUES (new.id, new.simple_name, new.canonical_fqn, new.namespace);
END;

CREATE TRIGGER IF NOT EXISTS symbols_ad AFTER DELETE ON symbols BEGIN
    INSERT INTO symbols_fts(symbols_fts, rowid, simple_name, canonical_fqn, namespace)
    VALUES ('delete', old.id, old.simple_name, old.canonical_fqn, old.namespace);
END;

CREATE TRIGGER IF NOT EXISTS symbols_au AFTER UPDATE ON symbols BEGIN
    INSERT INTO symbols_fts(symbols_fts, rowid, simple_name, canonical_fqn, namespace)
    VALUES ('delete', old.id, old.simple_name, old.canonical_fqn, old.namespace);
    INSERT INTO symbols_fts(rowid, simple_name, canonical_fqn, namespace)
    VALUES (new.id, new.simple_name, new.canonical_fqn, new.namespace);
END;

-- Indexes for common queries
CREATE INDEX IF NOT EXISTS idx_symbols_fqn ON symbols(canonical_fqn);
CREATE INDEX IF NOT EXISTS idx_symbols_simple_name ON symbols(simple_name);
CREATE INDEX IF NOT EXISTS idx_symbols_namespace ON symbols(namespace);
CREATE INDEX IF NOT EXISTS idx_symbols_flags ON symbols(flags);
CREATE INDEX IF NOT EXISTS idx_symbols_document ON symbols(document_id);
CREATE INDEX IF NOT EXISTS idx_symbols_solution ON symbols(solution_hash);
""";

        await ExecuteNonQueryAsync(schema, cancellationToken);
    }

    /// <summary>
    /// Build index from Roslyn solution.
    /// </summary>
    public async Task BuildFromSolutionAsync(Solution solution, CancellationToken cancellationToken) {
        if (_connection == null) {
            await InitializeAsync(cancellationToken);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Building SQLite symbol index from solution...");

        var solutionHash = ComputeSolutionHash(solution);

        // Check if index is up-to-date
        var existingHash = await GetStoredSolutionHashAsync(cancellationToken);
        if (existingHash == solutionHash) {
            TotalSymbols = await GetCountAsync(cancellationToken);
            _isBuilt = true;
            _logger.LogInformation(
            "SQLite symbol index is up-to-date with {Count} symbols",
            TotalSymbols);
            return;
        }

        // Clear old data for this solution
        await ClearAsync(cancellationToken);

        var seenFqns = new HashSet<string>(StringComparer.Ordinal);
        var batchSize = 1000;
        var entries = new List<(SerializableSymbolEntry Entry, string ProjectName)>(batchSize);
        var totalInserted = 0;

        foreach (var project in solution.Projects) {
            cancellationToken.ThrowIfCancellationRequested();

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
                continue;

            CollectSymbolsFromCompilation(
            compilation,
            project.Name,
            entries,
            seenFqns,
            solutionHash,
            cancellationToken);

            // Batch insert
            if (entries.Count >= batchSize) {
                await InsertBatchAsync(entries, solutionHash, cancellationToken);
                totalInserted += entries.Count;
                entries.Clear();

                _logger.LogDebug("Inserted {Count} symbols...", totalInserted);
            }
        }

        // Insert remaining
        if (entries.Count > 0) {
            await InsertBatchAsync(entries, solutionHash, cancellationToken);
            totalInserted += entries.Count;
        }

        TotalSymbols = totalInserted;
        _isBuilt = true;

        sw.Stop();
        _logger.LogInformation(
        "SQLite symbol index built: {Count} symbols in {ElapsedMs}ms. DB size: {Size}",
        TotalSymbols,
        sw.ElapsedMilliseconds,
        GetDbSizeFormatted());
    }

    private void CollectSymbolsFromCompilation(
    Compilation compilation,
    string projectName,
    List<(SerializableSymbolEntry, string)> entries,
    HashSet<string> seenFqns,
    string solutionHash,
    CancellationToken cancellationToken) {
        void VisitSymbol(ISymbol symbol) {
            cancellationToken.ThrowIfCancellationRequested();

            if (symbol.IsImplicitlyDeclared)
                return;
            if (symbol.Kind is SymbolKind.Alias or SymbolKind.ArrayType
                or SymbolKind.PointerType or SymbolKind.DynamicType)
                return;

            var fqn = symbol
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", "");

            if (!seenFqns.Add(fqn))
                return;

            try {
                var entry = CreateSerializableEntry(symbol, fqn, projectName);
                entries.Add((entry, projectName));
            } catch {
                // Skip problematic symbols
            }

            if (symbol is INamespaceOrTypeSymbol container) {
                foreach (var member in container.GetMembers()) {
                    VisitSymbol(member);
                }
            }
        }

        VisitSymbol(compilation.GlobalNamespace);
    }

    private static SerializableSymbolEntry CreateSerializableEntry(
    ISymbol symbol,
    string fqn,
    string projectName) {
        var simpleName = symbol.Name;
        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns == "<global namespace>")
            ns = "";

        return new SerializableSymbolEntry {
            CanonicalFqn = fqn,
            SimpleName = simpleName,
            Namespace = ns,
            Flags = (long)SymbolIndexEntryBuilder.ExtractFlags(symbol),
            NamespaceDepth = (byte)ns.Count(c => c == '.'),
            NameLength = (ushort)simpleName.Length,
            FqnLength = (ushort)fqn.Length,
            FirstChar = simpleName.Length > 0 ? char.ToLowerInvariant(simpleName[0]) : '\0',
            SimpleNameHashCode = StringComparer.OrdinalIgnoreCase.GetHashCode(simpleName),
            AssemblyName = symbol.ContainingAssembly?.Name ?? "",
            ProjectName = projectName,
        };
    }

    private async Task InsertBatchAsync(
    List<(SerializableSymbolEntry Entry, string ProjectName)> entries,
    string solutionHash,
    CancellationToken cancellationToken) {
        if (_connection == null || entries.Count == 0)
            return;

        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);

        try {
            await using var cmd = _connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = """
    INSERT INTO symbols (
        canonical_fqn, simple_name, namespace, flags, namespace_depth,
        name_length, fqn_length, first_char, simple_name_hash,
        assembly_name, project_name, solution_hash
    ) VALUES (
        $fqn, $name, $ns, $flags, $depth,
        $nameLen, $fqnLen, $firstChar, $hash,
        $assembly, $project, $solHash
    )
    """;

            var pFqn = cmd.Parameters.Add("$fqn", SqliteType.Text);
            var pName = cmd.Parameters.Add("$name", SqliteType.Text);
            var pNs = cmd.Parameters.Add("$ns", SqliteType.Text);
            var pFlags = cmd.Parameters.Add("$flags", SqliteType.Integer);
            var pDepth = cmd.Parameters.Add("$depth", SqliteType.Integer);
            var pNameLen = cmd.Parameters.Add("$nameLen", SqliteType.Integer);
            var pFqnLen = cmd.Parameters.Add("$fqnLen", SqliteType.Integer);
            var pFirstChar = cmd.Parameters.Add("$firstChar", SqliteType.Text);
            var pHash = cmd.Parameters.Add("$hash", SqliteType.Integer);
            var pAssembly = cmd.Parameters.Add("$assembly", SqliteType.Text);
            var pProject = cmd.Parameters.Add("$project", SqliteType.Text);
            var pSolHash = cmd.Parameters.Add("$solHash", SqliteType.Text);

            foreach (var (entry, _) in entries) {
                pFqn.Value = entry.CanonicalFqn;
                pName.Value = entry.SimpleName;
                pNs.Value = entry.Namespace;
                pFlags.Value = entry.Flags;
                pDepth.Value = entry.NamespaceDepth;
                pNameLen.Value = entry.NameLength;
                pFqnLen.Value = entry.FqnLength;
                pFirstChar.Value = entry.FirstChar.ToString();
                pHash.Value = entry.SimpleNameHashCode;
                pAssembly.Value = entry.AssemblyName;
                pProject.Value = entry.ProjectName;
                pSolHash.Value = solutionHash;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        } catch {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Fast symbol search using FTS5.
    /// </summary>
    public async Task<List<SymbolSearchResult>> FindAsync(
    string searchTerm,
    SymbolMetadataFlags requiredFlags = SymbolMetadataFlags.None,
    SymbolMetadataFlags excludedFlags = SymbolMetadataFlags.None,
    int limit = 100,
    CancellationToken cancellationToken = default) {
        if (_connection == null || string.IsNullOrWhiteSpace(searchTerm))
            return [];

        var results = new List<SymbolSearchResult>();

        // Use FTS5 MATCH for prefix search
        var ftsQuery = EscapeFtsQuery(searchTerm) + "*";

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
    SELECT s.id, s.canonical_fqn, s.simple_name, s.namespace, s.flags,
           s.namespace_depth, s.name_length, s.fqn_length, s.first_char,
           s.simple_name_hash, s.assembly_name, s.project_name,
           s.file_path, s.line_number,
           bm25(symbols_fts) as rank
    FROM symbols s
    JOIN symbols_fts fts ON s.id = fts.rowid
    WHERE symbols_fts MATCH $query
    """;

        // Add flag filters
        if (requiredFlags != SymbolMetadataFlags.None) {
            cmd.CommandText += " AND (s.flags & $reqFlags) = $reqFlags";
            cmd.Parameters.AddWithValue("$reqFlags", (long)requiredFlags);
        }

        if (excludedFlags != SymbolMetadataFlags.None) {
            cmd.CommandText += " AND (s.flags & $exclFlags) = 0";
            cmd.Parameters.AddWithValue("$exclFlags", (long)excludedFlags);
        }

        cmd.CommandText += " ORDER BY rank LIMIT $limit";
        cmd.Parameters.AddWithValue("$query", ftsQuery);
        cmd.Parameters.AddWithValue("$limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) {
            results.Add(ReadSymbolSearchResult(reader));
        }

        return results;
    }

    /// <summary>
    /// Find symbols by exact FQN.
    /// </summary>
    public async Task<SymbolSearchResult?> FindByFqnAsync(
    string fqn,
    CancellationToken cancellationToken = default) {
        if (_connection == null || string.IsNullOrWhiteSpace(fqn))
            return null;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
    SELECT id, canonical_fqn, simple_name, namespace, flags,
           namespace_depth, name_length, fqn_length, first_char,
           simple_name_hash, assembly_name, project_name,
           file_path, line_number, 0 as rank
    FROM symbols
    WHERE canonical_fqn = $fqn
    LIMIT 1
    """;
        cmd.Parameters.AddWithValue("$fqn", fqn);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken)) {
            return ReadSymbolSearchResult(reader);
        }

        return null;
    }

    /// <summary>
    /// Find all symbols in a namespace.
    /// </summary>
    public async Task<List<SymbolSearchResult>> FindByNamespaceAsync(
    string namespaceName,
    SymbolMetadataFlags requiredFlags = SymbolMetadataFlags.None,
    int limit = 1000,
    CancellationToken cancellationToken = default) {
        if (_connection == null)
            return [];

        var results = new List<SymbolSearchResult>();

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
    SELECT id, canonical_fqn, simple_name, namespace, flags,
           namespace_depth, name_length, fqn_length, first_char,
           simple_name_hash, assembly_name, project_name,
           file_path, line_number, 0 as rank
    FROM symbols
    WHERE namespace = $ns
    """;

        if (requiredFlags != SymbolMetadataFlags.None) {
            cmd.CommandText += " AND (flags & $flags) = $flags";
            cmd.Parameters.AddWithValue("$flags", (long)requiredFlags);
        }

        cmd.CommandText += " LIMIT $limit";
        cmd.Parameters.AddWithValue("$ns", namespaceName);
        cmd.Parameters.AddWithValue("$limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) {
            results.Add(ReadSymbolSearchResult(reader));
        }

        return results;
    }

    /// <summary>
    /// Incremental update: remove symbols for a document.
    /// </summary>
    public async Task RemoveDocumentAsync(
    string documentId,
    CancellationToken cancellationToken = default) {
        if (_connection == null || string.IsNullOrWhiteSpace(documentId))
            return;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM symbols WHERE document_id = $docId";
        cmd.Parameters.AddWithValue("$docId", documentId);

        var deleted = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (deleted > 0) {
            TotalSymbols -= deleted;
            _logger.LogDebug("Removed {Count} symbols for document {DocId}", deleted, documentId);
        }
    }

    /// <summary>
    /// Get statistics about the index.
    /// </summary>
    public async Task<SqliteSymbolIndexStats> GetStatisticsAsync(
    CancellationToken cancellationToken = default) {
        if (_connection == null)
            return new SqliteSymbolIndexStats();

        var totalSymbols = await GetCountAsync(cancellationToken);

        // Count by namespace
        await using var nsCmd = _connection.CreateCommand();
        nsCmd.CommandText = "SELECT COUNT(DISTINCT namespace) FROM symbols";
        var uniqueNamespaces = Convert.ToInt32(await nsCmd.ExecuteScalarAsync(cancellationToken));

        // Count by simple name
        await using var nameCmd = _connection.CreateCommand();
        nameCmd.CommandText = "SELECT COUNT(DISTINCT simple_name) FROM symbols";
        var uniqueSimpleNames = Convert.ToInt32(await nameCmd.ExecuteScalarAsync(cancellationToken));

        return new SqliteSymbolIndexStats {
            TotalSymbols = totalSymbols,
            UniqueNamespaces = uniqueNamespaces,
            UniqueSimpleNames = uniqueSimpleNames,
            DatabasePath = _dbPath,
            DatabaseSizeBytes = new FileInfo(_dbPath).Length,
        };
    }

    private async Task<int> GetCountAsync(CancellationToken cancellationToken) {
        if (_connection == null)
            return 0;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM symbols";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    private async Task ClearAsync(CancellationToken cancellationToken) {
        if (_connection == null)
            return;

        await ExecuteNonQueryAsync("DELETE FROM symbols", cancellationToken);
        TotalSymbols = 0;
        _hotCache.Clear();
    }

    private async Task<string?> GetStoredSolutionHashAsync(CancellationToken cancellationToken) {
        if (_connection == null)
            return null;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT solution_hash FROM symbols LIMIT 1";
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private static string ComputeSolutionHash(Solution solution) {
        // Simple hash based on project count and document count
        var projectCount = solution.Projects.Count();
        var documentCount = solution.Projects.Sum(p => p.Documents.Count());
        return $"{projectCount}:{documentCount}:{DateTime.UtcNow:yyyyMMdd}";
    }

    private string GetDbSizeFormatted() {
        try {
            var size = new FileInfo(_dbPath).Length;
            return size switch {
                < 1024 => $"{size} B",
                < 1024 * 1024 => $"{size / 1024.0:F1} KB",
                _ => $"{size / (1024.0 * 1024.0):F1} MB"
            };
        } catch {
            return "unknown";
        }
    }

    private async Task ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken) {
        if (_connection == null)
            return;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SymbolSearchResult ReadSymbolSearchResult(SqliteDataReader reader) {
        return new SymbolSearchResult {
            Id = reader.GetInt64(0),
            CanonicalFqn = reader.GetString(1),
            SimpleName = reader.GetString(2),
            Namespace = reader.GetString(3),
            Flags = (SymbolMetadataFlags)reader.GetInt64(4),
            NamespaceDepth = (byte)reader.GetInt32(5),
            NameLength = (ushort)reader.GetInt32(6),
            FqnLength = (ushort)reader.GetInt32(7),
            FirstChar = reader.GetString(8)[0],
            SimpleNameHashCode = reader.GetInt32(9),
            AssemblyName = reader.IsDBNull(10) ? null : reader.GetString(10),
            ProjectName = reader.IsDBNull(11) ? null : reader.GetString(11),
            FilePath = reader.IsDBNull(12) ? null : reader.GetString(12),
            LineNumber = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
            Rank = reader.GetDouble(14),
        };
    }

    private static string EscapeFtsQuery(string query) {
        // Escape FTS5 special characters
        return query
            .Replace("\"", "\"\"")
            .Replace("*", "")
            .Replace("?", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace(":", "")
            .Replace("-", " ");
    }

    public async ValueTask DisposeAsync() {
        if (_connection != null) {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}

/// <summary>
/// Search result from SQLite symbol index.
/// Lightweight DTO without ISymbol reference - resolve lazily when needed.
/// </summary>
public sealed record SymbolSearchResult {
    public required long Id { get; init; }
    public required string CanonicalFqn { get; init; }
    public required string SimpleName { get; init; }
    public required string Namespace { get; init; }
    public required SymbolMetadataFlags Flags { get; init; }
    public required byte NamespaceDepth { get; init; }
    public required ushort NameLength { get; init; }
    public required ushort FqnLength { get; init; }
    public required char FirstChar { get; init; }
    public required int SimpleNameHashCode { get; init; }
    public string? AssemblyName { get; init; }
    public string? ProjectName { get; init; }
    public string? FilePath { get; init; }
    public int LineNumber { get; init; }
    public double Rank { get; init; }
}

/// <summary>
/// Statistics for SQLite symbol index.
/// </summary>
public sealed record SqliteSymbolIndexStats {
    public int TotalSymbols { get; init; }
    public int UniqueNamespaces { get; init; }
    public int UniqueSimpleNames { get; init; }
    public string? DatabasePath { get; init; }
    public long DatabaseSizeBytes { get; init; }
}
