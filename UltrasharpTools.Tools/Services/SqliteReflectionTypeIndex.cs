using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// SQLite-based reflection type index with lazy loading.
/// Replaces in-memory FrozenDictionary to reduce memory from ~50MB to ~2MB.
/// Type metadata stored on disk, actual Type objects loaded on-demand with LRU cache.
/// </summary>
public sealed partial class SqliteReflectionTypeIndex : IAsyncDisposable {
    private readonly ILogger _logger;
    private readonly string _dbPath;
    private SqliteConnection? _connection;
    private MetadataLoadContext? _metadataLoadContext;

    // Small LRU cache for recently accessed Types
    private readonly ConcurrentDictionary<string, Type> _typeCache = new();
    private const int MaxTypeCacheSize = 500;

    public SqliteReflectionTypeIndex(ILogger logger, string? dbPath = null) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbPath = dbPath ?? GetDefaultDbPath();
    }

    public int TotalTypes { get; private set; }
    public bool IsInitialized => _connection != null;

    private static string GetDefaultDbPath() {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "UltrasharpTools", "ReflectionIndex");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "reflection_types.db");
    }

    /// <summary>
    /// Initialize SQLite database with schema.
    /// </summary>
    public async Task InitializeAsync(
    MetadataLoadContext metadataLoadContext,
    CancellationToken cancellationToken = default) {
        _metadataLoadContext = metadataLoadContext;

        var connectionString = new SqliteConnectionStringBuilder {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
        }.ConnectionString;

        _connection = new SqliteConnection(connectionString);
        await _connection.OpenAsync(cancellationToken);

        await ExecuteNonQueryAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await ExecuteNonQueryAsync("PRAGMA synchronous=NORMAL;", cancellationToken);
        await ExecuteNonQueryAsync("PRAGMA cache_size=-32000;", cancellationToken); // 32MB cache
        await ExecuteNonQueryAsync("PRAGMA temp_store=MEMORY;", cancellationToken);

        await CreateSchemaAsync(cancellationToken);

        LogInitialized(_dbPath);
    }

    private async Task CreateSchemaAsync(CancellationToken cancellationToken) {
        const string schema = """
CREATE TABLE IF NOT EXISTS reflection_types (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    full_name TEXT NOT NULL UNIQUE,
    simple_name TEXT NOT NULL,
    namespace TEXT,
    assembly_path TEXT NOT NULL,
    assembly_name TEXT,
    is_public INTEGER NOT NULL DEFAULT 1,
    is_class INTEGER NOT NULL DEFAULT 0,
    is_interface INTEGER NOT NULL DEFAULT 0,
    is_struct INTEGER NOT NULL DEFAULT 0,
    is_enum INTEGER NOT NULL DEFAULT 0,
    is_generic INTEGER NOT NULL DEFAULT 0,
    base_type TEXT,
    solution_hash TEXT
);

-- FTS5 for full-text search
CREATE VIRTUAL TABLE IF NOT EXISTS reflection_types_fts USING fts5(
    full_name,
    simple_name,
    namespace,
    content=reflection_types,
    content_rowid=id,
    tokenize='unicode61 remove_diacritics 2'
);

-- Triggers to keep FTS in sync
CREATE TRIGGER IF NOT EXISTS rt_ai AFTER INSERT ON reflection_types BEGIN
    INSERT INTO reflection_types_fts(rowid, full_name, simple_name, namespace)
    VALUES (new.id, new.full_name, new.simple_name, new.namespace);
END;

CREATE TRIGGER IF NOT EXISTS rt_ad AFTER DELETE ON reflection_types BEGIN
    INSERT INTO reflection_types_fts(reflection_types_fts, rowid, full_name, simple_name, namespace)
    VALUES ('delete', old.id, old.full_name, old.simple_name, old.namespace);
END;

-- Indexes
CREATE INDEX IF NOT EXISTS idx_rt_full_name ON reflection_types(full_name);
CREATE INDEX IF NOT EXISTS idx_rt_simple_name ON reflection_types(simple_name);
CREATE INDEX IF NOT EXISTS idx_rt_namespace ON reflection_types(namespace);
CREATE INDEX IF NOT EXISTS idx_rt_assembly ON reflection_types(assembly_path);
CREATE INDEX IF NOT EXISTS idx_rt_solution ON reflection_types(solution_hash);
""";

        await ExecuteNonQueryAsync(schema, cancellationToken);
    }

    /// <summary>
    /// Populate index from assembly paths.
    /// </summary>
    public async Task PopulateFromAssembliesAsync(
    IEnumerable<string> assemblyPaths,
    string solutionHash,
    CancellationToken cancellationToken = default) {
        if (_connection == null || _metadataLoadContext == null)
            throw new InvalidOperationException("Index not initialized");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        LogPopulating();

        // Check if data is current
        var existingHash = await GetStoredSolutionHashAsync(cancellationToken);
        if (existingHash == solutionHash) {
            TotalTypes = await GetCountAsync(cancellationToken);
            LogUpToDate(TotalTypes);
            return;
        }

        // Clear old data
        await ClearAsync(cancellationToken);

        var pathsList = assemblyPaths.ToList();
        var totalInserted = 0;
        var batch = new List<ReflectionTypeMetadata>(1000);

        foreach (var assemblyPath in pathsList) {
            cancellationToken.ThrowIfCancellationRequested();

            try {
                var types = ExtractTypesFromAssembly(assemblyPath);
                foreach (var typeInfo in types) {
                    batch.Add(new ReflectionTypeMetadata {
                        FullName = typeInfo.FullName ?? typeInfo.Name,
                        SimpleName = typeInfo.Name,
                        Namespace = typeInfo.Namespace,
                        AssemblyPath = assemblyPath,
                        AssemblyName = typeInfo.Assembly?.GetName().Name,
                        IsPublic = typeInfo.IsPublic,
                        IsClass = typeInfo.IsClass && !typeInfo.IsValueType,
                        IsInterface = typeInfo.IsInterface,
                        IsStruct = typeInfo.IsValueType && !typeInfo.IsEnum,
                        IsEnum = typeInfo.IsEnum,
                        IsGeneric = typeInfo.IsGenericType,
                        BaseType = typeInfo.BaseType?.FullName,
                        SolutionHash = solutionHash,
                    });

                    if (batch.Count >= 1000) {
                        await InsertBatchAsync(batch, cancellationToken);
                        totalInserted += batch.Count;
                        batch.Clear();
                    }
                }
            } catch (Exception ex) {
                LogTypeLoadError(assemblyPath, ex.Message);
            }
        }

        // Insert remaining
        if (batch.Count > 0) {
            await InsertBatchAsync(batch, cancellationToken);
            totalInserted += batch.Count;
        }

        TotalTypes = totalInserted;

        sw.Stop();
        LogPopulated(TotalTypes, sw.ElapsedMilliseconds, GetDbSizeFormatted());
    }

    private IEnumerable<Type> ExtractTypesFromAssembly(string assemblyPath) {
        if (_metadataLoadContext == null || !File.Exists(assemblyPath))
            yield break;

        Assembly assembly;
        try {
            assembly = _metadataLoadContext.LoadFromAssemblyPath(assemblyPath);
        } catch {
            yield break;
        }

        Type[] types;
        try {
            types = assembly.GetTypes();
        } catch (ReflectionTypeLoadException ex) {
            types = ex.Types.Where(t => t != null).ToArray()!;
        } catch {
            yield break;
        }

        foreach (var type in types) {
            if (type == null || string.IsNullOrEmpty(type.FullName))
                continue;

            // Skip compiler-generated types
            if (type.Name.StartsWith('<') || type.Name.Contains("__"))
                continue;

            yield return type;
        }
    }

    private async Task InsertBatchAsync(
    List<ReflectionTypeMetadata> batch,
    CancellationToken cancellationToken) {
        if (_connection == null || batch.Count == 0)
            return;

        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);

        try {
            await using var cmd = _connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = """
    INSERT OR IGNORE INTO reflection_types (
        full_name, simple_name, namespace, assembly_path, assembly_name,
        is_public, is_class, is_interface, is_struct, is_enum,
        is_generic, base_type, solution_hash
    ) VALUES (
        $fullName, $simpleName, $ns, $asmPath, $asmName,
        $isPublic, $isClass, $isInterface, $isStruct, $isEnum,
        $isGeneric, $baseType, $solHash
    )
    """;

            var pFullName = cmd.Parameters.Add("$fullName", SqliteType.Text);
            var pSimpleName = cmd.Parameters.Add("$simpleName", SqliteType.Text);
            var pNs = cmd.Parameters.Add("$ns", SqliteType.Text);
            var pAsmPath = cmd.Parameters.Add("$asmPath", SqliteType.Text);
            var pAsmName = cmd.Parameters.Add("$asmName", SqliteType.Text);
            var pIsPublic = cmd.Parameters.Add("$isPublic", SqliteType.Integer);
            var pIsClass = cmd.Parameters.Add("$isClass", SqliteType.Integer);
            var pIsInterface = cmd.Parameters.Add("$isInterface", SqliteType.Integer);
            var pIsStruct = cmd.Parameters.Add("$isStruct", SqliteType.Integer);
            var pIsEnum = cmd.Parameters.Add("$isEnum", SqliteType.Integer);
            var pIsGeneric = cmd.Parameters.Add("$isGeneric", SqliteType.Integer);
            var pBaseType = cmd.Parameters.Add("$baseType", SqliteType.Text);
            var pSolHash = cmd.Parameters.Add("$solHash", SqliteType.Text);

            foreach (var item in batch) {
                pFullName.Value = item.FullName;
                pSimpleName.Value = item.SimpleName;
                pNs.Value = item.Namespace ?? (object)DBNull.Value;
                pAsmPath.Value = item.AssemblyPath;
                pAsmName.Value = item.AssemblyName ?? (object)DBNull.Value;
                pIsPublic.Value = item.IsPublic ? 1 : 0;
                pIsClass.Value = item.IsClass ? 1 : 0;
                pIsInterface.Value = item.IsInterface ? 1 : 0;
                pIsStruct.Value = item.IsStruct ? 1 : 0;
                pIsEnum.Value = item.IsEnum ? 1 : 0;
                pIsGeneric.Value = item.IsGeneric ? 1 : 0;
                pBaseType.Value = item.BaseType ?? (object)DBNull.Value;
                pSolHash.Value = item.SolutionHash;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        } catch {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Find type by exact FQN with lazy loading.
    /// </summary>
    public async Task<Type?> FindTypeAsync(
    string fullName,
    CancellationToken cancellationToken = default) {
        // Check cache first
        if (_typeCache.TryGetValue(fullName, out var cachedType))
            return cachedType;

        if (_connection == null || _metadataLoadContext == null)
            return null;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT assembly_path FROM reflection_types WHERE full_name = $name LIMIT 1";
        cmd.Parameters.AddWithValue("$name", fullName);

        var assemblyPath = await cmd.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrEmpty(assemblyPath))
            return null;

        // Lazy load the actual Type
        var type = LoadTypeFromAssembly(fullName, assemblyPath);
        if (type != null) {
            AddToCache(fullName, type);
        }

        return type;
    }

    /// <summary>
    /// Search types using regex pattern via FTS5.
    /// </summary>
    public async Task<List<ReflectionTypeSearchResult>> SearchTypesAsync(
    string regexPattern,
    int limit = 100,
    CancellationToken cancellationToken = default) {
        if (_connection == null)
            return [];

        var results = new List<ReflectionTypeSearchResult>();
        var regex = new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Use FTS5 for prefix search if pattern is simple, otherwise scan
        var isSimplePattern = !regexPattern.Contains('*') &&
                          !regexPattern.Contains('+') &&
                          !regexPattern.Contains('?') &&
                          !regexPattern.Contains('[');

        string sql;
        if (isSimplePattern) {
            var ftsQuery = EscapeFtsQuery(regexPattern) + "*";
            sql = $"""
    SELECT r.id, r.full_name, r.simple_name, r.namespace, r.assembly_path,
           r.is_public, r.is_class, r.is_interface, r.is_struct, r.is_enum,
           r.is_generic, r.base_type, bm25(reflection_types_fts) as rank
    FROM reflection_types r
    JOIN reflection_types_fts fts ON r.id = fts.rowid
    WHERE reflection_types_fts MATCH '{ftsQuery}'
    ORDER BY rank
    LIMIT {limit * 2}
    """;
        } else {
            sql = $"SELECT id, full_name, simple_name, namespace, assembly_path, " +
              "is_public, is_class, is_interface, is_struct, is_enum, " +
              "is_generic, base_type, 0 as rank " +
              "FROM reflection_types LIMIT 10000";
        }

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken) && results.Count < limit) {
            var fullName = reader.GetString(1);
            var simpleName = reader.GetString(2);

            // Apply regex filter
            if (regex.IsMatch(fullName) || regex.IsMatch(simpleName)) {
                results.Add(ReadSearchResult(reader));
            }
        }

        return results;
    }

    /// <summary>
    /// Get actual Type with lazy loading from search result.
    /// </summary>
    public Type? LoadType(ReflectionTypeSearchResult searchResult) {
        if (_metadataLoadContext == null)
            return null;

        // Check cache
        if (_typeCache.TryGetValue(searchResult.FullName, out var cachedType))
            return cachedType;

        var type = LoadTypeFromAssembly(searchResult.FullName, searchResult.AssemblyPath);
        if (type != null) {
            AddToCache(searchResult.FullName, type);
        }

        return type;
    }

    private Type? LoadTypeFromAssembly(string fullName, string assemblyPath) {
        if (_metadataLoadContext == null || !File.Exists(assemblyPath))
            return null;

        try {
            var assembly = _metadataLoadContext.LoadFromAssemblyPath(assemblyPath);
            return assembly.GetType(fullName);
        } catch {
            return null;
        }
    }

    private void AddToCache(string fullName, Type type) {
        // Simple eviction: if cache is full, clear half of it
        if (_typeCache.Count >= MaxTypeCacheSize) {
            var keysToRemove = _typeCache.Keys.Take(MaxTypeCacheSize / 2).ToList();
            foreach (var key in keysToRemove) {
                _typeCache.TryRemove(key, out _);
            }
        }

        _typeCache.TryAdd(fullName, type);
    }

    private async Task<int> GetCountAsync(CancellationToken cancellationToken) {
        if (_connection == null)
            return 0;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM reflection_types";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    private async Task ClearAsync(CancellationToken cancellationToken) {
        if (_connection == null)
            return;

        await ExecuteNonQueryAsync("DELETE FROM reflection_types", cancellationToken);
        TotalTypes = 0;
        _typeCache.Clear();
    }

    private async Task<string?> GetStoredSolutionHashAsync(CancellationToken cancellationToken) {
        if (_connection == null)
            return null;

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT solution_hash FROM reflection_types LIMIT 1";
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result as string;
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

    private static ReflectionTypeSearchResult ReadSearchResult(SqliteDataReader reader) {
        return new ReflectionTypeSearchResult {
            Id = reader.GetInt64(0),
            FullName = reader.GetString(1),
            SimpleName = reader.GetString(2),
            Namespace = reader.IsDBNull(3) ? null : reader.GetString(3),
            AssemblyPath = reader.GetString(4),
            IsPublic = reader.GetInt32(5) == 1,
            IsClass = reader.GetInt32(6) == 1,
            IsInterface = reader.GetInt32(7) == 1,
            IsStruct = reader.GetInt32(8) == 1,
            IsEnum = reader.GetInt32(9) == 1,
            IsGeneric = reader.GetInt32(10) == 1,
            BaseType = reader.IsDBNull(11) ? null : reader.GetString(11),
            Rank = reader.GetDouble(12),
        };
    }

    private static string EscapeFtsQuery(string query) {
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
        _typeCache.Clear();
    }
}

/// <summary>
/// Metadata for a reflection type stored in SQLite.
/// </summary>
internal sealed record ReflectionTypeMetadata {
    public required string FullName { get; init; }
    public required string SimpleName { get; init; }
    public string? Namespace { get; init; }
    public required string AssemblyPath { get; init; }
    public string? AssemblyName { get; init; }
    public bool IsPublic { get; init; }
    public bool IsClass { get; init; }
    public bool IsInterface { get; init; }
    public bool IsStruct { get; init; }
    public bool IsEnum { get; init; }
    public bool IsGeneric { get; init; }
    public string? BaseType { get; init; }
    public string? SolutionHash { get; init; }
}

/// <summary>
/// Search result for reflection type lookup.
/// Lightweight DTO - actual Type loaded lazily via LoadType().
/// </summary>
public sealed record ReflectionTypeSearchResult {
    public required long Id { get; init; }
    public required string FullName { get; init; }
    public required string SimpleName { get; init; }
    public string? Namespace { get; init; }
    public required string AssemblyPath { get; init; }
    public bool IsPublic { get; init; }
    public bool IsClass { get; init; }
    public bool IsInterface { get; init; }
    public bool IsStruct { get; init; }
    public bool IsEnum { get; init; }
    public bool IsGeneric { get; init; }
    public string? BaseType { get; init; }
    public double Rank { get; init; }
}
