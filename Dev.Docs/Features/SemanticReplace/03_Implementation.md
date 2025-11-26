# Semantic Replace - Implementation Plan

## Фазы реализации

### Phase 1: Models & Interfaces (1 день)

**Цель**: Определить модели данных и интерфейсы.

#### Файлы

```
UltrasharpTools.Tools/Replace/
├─ Models/
│  ├─ CodeMatch.cs
│  ├─ CodeReplacement.cs
│  ├─ ReplaceResult.cs
│  ├─ ReplaceScope.cs
│  └─ SearchMode.cs
│
└─ Interfaces/
   ├─ IPatternMatcherService.cs
   ├─ IContextExtractorService.cs
   ├─ IBatchReplacerService.cs
   └─ ISemanticReplaceService.cs
```

#### CodeMatch.cs

```csharp
namespace UltrasharpTools.Tools.Replace.Models;

/// <summary>
/// Результат поиска паттерна с полным контекстом
/// </summary>
public sealed record CodeMatch
{
    /// <summary>Уникальный ID для референса в apply</summary>
    public required string Id { get; init; }

    /// <summary>FQN контейнера (метод, класс)</summary>
    public required string ContainerFqn { get; init; }

    /// <summary>Путь к файлу</summary>
    public required string FilePath { get; init; }

    /// <summary>Строка где найден паттерн</summary>
    public required int MatchLine { get; init; }

    /// <summary>Колонка где найден паттерн</summary>
    public required int MatchColumn { get; init; }

    /// <summary>Полный код контейнера (scope)</summary>
    public required string FullCode { get; init; }

    /// <summary>Фрагмент где найден паттерн</summary>
    public required string MatchFragment { get; init; }

    /// <summary>Тип контейнера</summary>
    public required CodeUnitType ContainerType { get; init; }

    /// <summary>Начальная строка контейнера в файле</summary>
    public required int ContainerStartLine { get; init; }

    /// <summary>Конечная строка контейнера в файле</summary>
    public required int ContainerEndLine { get; init; }

    /// <summary>Метаданные (зависимости, используемые типы)</summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>Semantic similarity score (только для semantic search)</summary>
    public float? SemanticSimilarity { get; init; }
}

public enum ReplaceScope
{
    Statement,   // Только statement
    Block,       // Весь блок (if/while/try)
    Member,      // Весь метод/property
    Type,        // Весь класс
    File         // Весь файл
}

public enum SearchMode
{
    Regex,       // Regex по тексту
    Roslyn,      // Roslyn semantic search
    Semantic     // Embedding-based search
}
```

#### ISemanticReplaceService.cs

```csharp
namespace UltrasharpTools.Tools.Replace.Interfaces;

public interface ISemanticReplaceService
{
    /// <summary>
    /// Найти все вхождения паттерна с контекстом
    /// </summary>
    Task<SemanticReplacePreviewResult> PreviewAsync(
        string pattern,
        SearchMode searchMode,
        ReplaceScope scope,
        string? filePattern = null,
        string? namespaceFilter = null,
        int limit = 100,
        int offset = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Применить batch изменений
    /// </summary>
    Task<ReplaceResult> ApplyAsync(
        IReadOnlyList<CodeReplacement> replacements,
        ApplyMode mode = ApplyMode.AllOrNothing,
        string? commitMessage = null,
        CancellationToken ct = default);

    /// <summary>
    /// Применить семантическую трансформацию
    /// </summary>
    Task<ReplaceResult> TransformAsync(
        string pattern,
        SearchMode searchMode,
        ReplaceScope scope,
        string transformation,
        string? filePattern = null,
        string? namespaceFilter = null,
        string? commitMessage = null,
        CancellationToken ct = default);
}

public enum ApplyMode
{
    AllOrNothing,   // Откат всего при любой ошибке
    BestEffort      // Применить успешные, пропустить failed
}
```

---

### Phase 2: Pattern Matching (2 дня)

**Цель**: Реализовать три режима поиска.

#### PatternMatcherService.cs

```csharp
namespace UltrasharpTools.Tools.Replace.Services;

public sealed partial class PatternMatcherService : IPatternMatcherService
{
    private readonly ISolutionManager _solutionManager;
    private readonly IFastSymbolIndex _symbolIndex;
    private readonly ISemanticSimilarityService? _semanticService;
    private readonly ILogger<PatternMatcherService> _logger;

    public PatternMatcherService(
        ISolutionManager solutionManager,
        IFastSymbolIndex symbolIndex,
        ISemanticSimilarityService? semanticService,
        ILogger<PatternMatcherService> logger)
    {
        _solutionManager = solutionManager;
        _symbolIndex = symbolIndex;
        _semanticService = semanticService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PatternMatch>> FindMatchesAsync(
        string pattern,
        SearchMode mode,
        string? filePattern,
        string? namespaceFilter,
        int limit,
        CancellationToken ct)
    {
        return mode switch
        {
            SearchMode.Regex => await FindRegexMatchesAsync(pattern, filePattern, namespaceFilter, limit, ct),
            SearchMode.Roslyn => await FindRoslynMatchesAsync(pattern, filePattern, namespaceFilter, limit, ct),
            SearchMode.Semantic => await FindSemanticMatchesAsync(pattern, filePattern, namespaceFilter, limit, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private async Task<IReadOnlyList<PatternMatch>> FindRegexMatchesAsync(
        string pattern,
        string? filePattern,
        string? namespaceFilter,
        int limit,
        CancellationToken ct)
    {
        var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.Multiline);
        var matches = new List<PatternMatch>();

        var solution = _solutionManager.CurrentSolution
            ?? throw new InvalidOperationException("No solution loaded");

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                ct.ThrowIfCancellationRequested();

                if (!MatchesFilePattern(document.FilePath, filePattern))
                    continue;

                var text = await document.GetTextAsync(ct);
                var content = text.ToString();

                foreach (Match regexMatch in regex.Matches(content))
                {
                    var linePosition = text.Lines.GetLinePosition(regexMatch.Index);

                    // Проверка namespace filter через syntax tree
                    if (namespaceFilter != null)
                    {
                        var syntaxTree = await document.GetSyntaxTreeAsync(ct);
                        var root = await syntaxTree!.GetRootAsync(ct);
                        var node = root.FindToken(regexMatch.Index).Parent;

                        if (!IsInNamespace(node, namespaceFilter))
                            continue;
                    }

                    matches.Add(new PatternMatch
                    {
                        DocumentId = document.Id,
                        FilePath = document.FilePath!,
                        StartPosition = regexMatch.Index,
                        Length = regexMatch.Length,
                        Line = linePosition.Line + 1,
                        Column = linePosition.Character + 1,
                        MatchedText = regexMatch.Value
                    });

                    if (matches.Count >= limit)
                        return matches;
                }
            }
        }

        LogFoundMatches(matches.Count, SearchMode.Regex.ToString());
        return matches;
    }

    private async Task<IReadOnlyList<PatternMatch>> FindRoslynMatchesAsync(
        string pattern,
        string? filePattern,
        string? namespaceFilter,
        int limit,
        CancellationToken ct)
    {
        var matches = new List<PatternMatch>();

        // Попробовать найти как FQN через FastSymbolIndex
        var symbols = await _symbolIndex.FindSymbolsAsync(pattern, limit, ct);

        foreach (var symbol in symbols)
        {
            ct.ThrowIfCancellationRequested();

            if (namespaceFilter != null && !symbol.ContainingNamespace.ToDisplayString().StartsWith(namespaceFilter.TrimEnd('*')))
                continue;

            foreach (var location in symbol.Locations)
            {
                if (!location.IsInSource)
                    continue;

                var filePath = location.SourceTree?.FilePath;
                if (filePath == null || !MatchesFilePattern(filePath, filePattern))
                    continue;

                var span = location.GetLineSpan();

                matches.Add(new PatternMatch
                {
                    DocumentId = GetDocumentId(filePath),
                    FilePath = filePath,
                    StartPosition = location.SourceSpan.Start,
                    Length = location.SourceSpan.Length,
                    Line = span.StartLinePosition.Line + 1,
                    Column = span.StartLinePosition.Character + 1,
                    MatchedText = symbol.ToDisplayString(),
                    Symbol = symbol
                });

                if (matches.Count >= limit)
                    return matches;
            }
        }

        LogFoundMatches(matches.Count, SearchMode.Roslyn.ToString());
        return matches;
    }

    private async Task<IReadOnlyList<PatternMatch>> FindSemanticMatchesAsync(
        string pattern,
        string? filePattern,
        string? namespaceFilter,
        int limit,
        CancellationToken ct)
    {
        if (_semanticService == null)
            throw new InvalidOperationException("Semantic search is not available. Enable semantic mode first.");

        var searchResults = await _semanticService.SearchAsync(
            pattern,
            topK: limit,
            minSimilarity: 0.7f,
            ct);

        var matches = new List<PatternMatch>();

        foreach (var result in searchResults)
        {
            ct.ThrowIfCancellationRequested();

            if (!MatchesFilePattern(result.FilePath, filePattern))
                continue;

            if (namespaceFilter != null && !result.Fqn.StartsWith(namespaceFilter.TrimEnd('*')))
                continue;

            matches.Add(new PatternMatch
            {
                DocumentId = GetDocumentId(result.FilePath),
                FilePath = result.FilePath,
                StartPosition = result.StartPosition,
                Length = result.Length,
                Line = result.Line,
                Column = 1,
                MatchedText = result.Fqn,
                SemanticSimilarity = result.Similarity
            });
        }

        LogFoundMatches(matches.Count, SearchMode.Semantic.ToString());
        return matches;
    }

    private bool MatchesFilePattern(string? filePath, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(filePath))
            return true;

        // Простой glob matching
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^/\\\\]*")
            .Replace("\\?", ".") + "$";

        return Regex.IsMatch(filePath, regexPattern, RegexOptions.IgnoreCase);
    }
}
```

---

### Phase 3: Context Extraction (2 дня)

**Цель**: Извлечение полного контекста для каждого match.

#### ContextExtractorService.cs

```csharp
namespace UltrasharpTools.Tools.Replace.Services;

public sealed partial class ContextExtractorService : IContextExtractorService
{
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger<ContextExtractorService> _logger;

    public ContextExtractorService(
        ISolutionManager solutionManager,
        ILogger<ContextExtractorService> logger)
    {
        _solutionManager = solutionManager;
        _logger = logger;
    }

    public async Task<CodeMatch> ExtractContextAsync(
        PatternMatch match,
        ReplaceScope scope,
        CancellationToken ct)
    {
        var document = _solutionManager.CurrentSolution!.GetDocument(match.DocumentId)
            ?? throw new InvalidOperationException($"Document not found: {match.DocumentId}");

        var syntaxTree = await document.GetSyntaxTreeAsync(ct);
        var root = await syntaxTree!.GetRootAsync(ct);
        var semanticModel = await document.GetSemanticModelAsync(ct);

        // Найти syntax node в позиции match
        var token = root.FindToken(match.StartPosition);
        var node = token.Parent;

        // Расширить до требуемого scope
        var containerNode = ExpandToScope(node!, scope);
        var containerSymbol = semanticModel!.GetDeclaredSymbol(containerNode)
            ?? semanticModel.GetSymbolInfo(containerNode).Symbol;

        // Извлечь метаданные
        var metadata = ExtractMetadata(containerNode, semanticModel);

        var lineSpan = containerNode.GetLocation().GetLineSpan();

        return new CodeMatch
        {
            Id = GenerateMatchId(match),
            ContainerFqn = containerSymbol?.ToDisplayString() ?? GetNodeName(containerNode),
            FilePath = match.FilePath,
            MatchLine = match.Line,
            MatchColumn = match.Column,
            FullCode = containerNode.ToFullString().TrimIndentation(),
            MatchFragment = match.MatchedText,
            ContainerType = GetCodeUnitType(containerNode),
            ContainerStartLine = lineSpan.StartLinePosition.Line + 1,
            ContainerEndLine = lineSpan.EndLinePosition.Line + 1,
            Metadata = metadata,
            SemanticSimilarity = match.SemanticSimilarity
        };
    }

    private SyntaxNode ExpandToScope(SyntaxNode node, ReplaceScope scope)
    {
        return scope switch
        {
            ReplaceScope.Statement => FindContainingStatement(node),
            ReplaceScope.Block => FindContainingBlock(node),
            ReplaceScope.Member => FindContainingMember(node),
            ReplaceScope.Type => FindContainingType(node),
            ReplaceScope.File => node.SyntaxTree.GetRoot(),
            _ => node
        };
    }

    private SyntaxNode FindContainingStatement(SyntaxNode node)
    {
        return node.AncestorsAndSelf()
            .FirstOrDefault(n => n is StatementSyntax)
            ?? node;
    }

    private SyntaxNode FindContainingBlock(SyntaxNode node)
    {
        return node.AncestorsAndSelf()
            .FirstOrDefault(n => n is BlockSyntax or SwitchSectionSyntax
                or IfStatementSyntax or WhileStatementSyntax
                or ForStatementSyntax or ForEachStatementSyntax
                or TryStatementSyntax or CatchClauseSyntax)
            ?? FindContainingStatement(node);
    }

    private SyntaxNode FindContainingMember(SyntaxNode node)
    {
        return node.AncestorsAndSelf()
            .FirstOrDefault(n => n is MemberDeclarationSyntax
                or LocalFunctionStatementSyntax
                or AccessorDeclarationSyntax)
            ?? node;
    }

    private SyntaxNode FindContainingType(SyntaxNode node)
    {
        return node.AncestorsAndSelf()
            .FirstOrDefault(n => n is TypeDeclarationSyntax)
            ?? node;
    }

    private Dictionary<string, object> ExtractMetadata(
        SyntaxNode containerNode,
        SemanticModel semanticModel)
    {
        var metadata = new Dictionary<string, object>();

        // Для методов
        if (containerNode is MethodDeclarationSyntax method)
        {
            metadata["parameters"] = method.ParameterList.Parameters
                .Select(p => $"{p.Type} {p.Identifier}")
                .ToList();

            metadata["returnType"] = method.ReturnType.ToString();
            metadata["isAsync"] = method.Modifiers.Any(SyntaxKind.AsyncKeyword);

            // Проверить наличие ILogger field в родительском классе
            var containingType = method.Parent as TypeDeclarationSyntax;
            if (containingType != null)
            {
                var loggerField = containingType.Members
                    .OfType<FieldDeclarationSyntax>()
                    .FirstOrDefault(f => f.Declaration.Type.ToString().Contains("ILogger"));

                metadata["hasILoggerField"] = loggerField != null;
                if (loggerField != null)
                {
                    metadata["loggerFieldName"] = loggerField.Declaration.Variables.First().Identifier.Text;
                }
            }
        }

        // Для классов
        if (containerNode is TypeDeclarationSyntax typeDecl)
        {
            var constructor = typeDecl.Members
                .OfType<ConstructorDeclarationSyntax>()
                .FirstOrDefault();

            if (constructor != null)
            {
                metadata["constructorParams"] = constructor.ParameterList.Parameters
                    .Select(p => $"{p.Type} {p.Identifier}")
                    .ToList();
            }

            // Используемые типы
            var usedTypes = new HashSet<string>();
            foreach (var identifier in containerNode.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                if (symbol is ITypeSymbol typeSymbol)
                {
                    usedTypes.Add(typeSymbol.ToDisplayString());
                }
            }
            metadata["usedTypes"] = usedTypes.ToList();
        }

        return metadata;
    }

    private static string GenerateMatchId(PatternMatch match)
    {
        var hash = XxHash64.HashToUInt64(
            Encoding.UTF8.GetBytes($"{match.FilePath}:{match.Line}:{match.Column}:{match.MatchedText}"));
        return $"sr-{hash:x8}";
    }

    private static CodeUnitType GetCodeUnitType(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax => CodeUnitType.Method,
        PropertyDeclarationSyntax => CodeUnitType.Property,
        FieldDeclarationSyntax => CodeUnitType.Field,
        ClassDeclarationSyntax => CodeUnitType.Type,
        InterfaceDeclarationSyntax => CodeUnitType.Type,
        StructDeclarationSyntax => CodeUnitType.Type,
        RecordDeclarationSyntax => CodeUnitType.Type,
        NamespaceDeclarationSyntax => CodeUnitType.Namespace,
        CompilationUnitSyntax => CodeUnitType.File,
        BlockSyntax => CodeUnitType.Block,
        _ => CodeUnitType.Statement
    };
}
```

---

### Phase 4: Batch Replacer (2 дня)

**Цель**: Атомарное применение batch изменений.

#### BatchReplacerService.cs

```csharp
namespace UltrasharpTools.Tools.Replace.Services;

public sealed partial class BatchReplacerService : IBatchReplacerService
{
    private readonly ISolutionManager _solutionManager;
    private readonly ICodeModificationService _modificationService;
    private readonly IFormattingService _formattingService;
    private readonly IDiagnosticService _diagnosticService;
    private readonly IGitService? _gitService;
    private readonly ISnapshotService _snapshotService;
    private readonly ILogger<BatchReplacerService> _logger;

    // Registry для tracking matches между preview и apply
    private readonly ConcurrentDictionary<string, CodeMatch> _matchRegistry = new();

    public void RegisterMatches(IEnumerable<CodeMatch> matches)
    {
        foreach (var match in matches)
        {
            _matchRegistry[match.Id] = match;
        }
    }

    public async Task<ReplaceResult> ApplyReplacementsAsync(
        IReadOnlyList<CodeReplacement> replacements,
        ApplyMode mode,
        string? commitMessage,
        CancellationToken ct)
    {
        LogApplyStarted(replacements.Count, mode.ToString());

        // 1. Валидация
        var validationResult = await ValidateReplacementsAsync(replacements, ct);
        if (!validationResult.IsValid)
        {
            return ReplaceResult.ValidationFailed(validationResult.Errors);
        }

        // 2. Проверка на конфликты (overlapping changes)
        var conflicts = DetectConflicts(replacements);
        if (conflicts.Any())
        {
            return ReplaceResult.ConflictDetected(conflicts);
        }

        // 3. Группировка по файлам
        var byFile = replacements
            .GroupBy(r => _matchRegistry[r.MatchId].FilePath)
            .OrderBy(g => g.Key)
            .ToList();

        // 4. Snapshot
        var snapshotId = await _snapshotService.CreateSnapshotAsync(
            "before-semantic-replace",
            byFile.Select(g => g.Key).ToList(),
            ct);

        var appliedChanges = new List<AppliedChange>();
        var failedChanges = new List<FailedChange>();

        try
        {
            // 5. Применение по файлам
            foreach (var fileGroup in byFile)
            {
                ct.ThrowIfCancellationRequested();

                var fileResult = await ApplyFileReplacementsAsync(
                    fileGroup.Key,
                    fileGroup.ToList(),
                    ct);

                appliedChanges.AddRange(fileResult.Applied);
                failedChanges.AddRange(fileResult.Failed);

                if (mode == ApplyMode.AllOrNothing && fileResult.Failed.Any())
                {
                    await _snapshotService.RollbackAsync(snapshotId, ct);
                    return ReplaceResult.Failed(
                        "Rollback due to AllOrNothing mode",
                        appliedChanges,
                        failedChanges);
                }
            }

            // 6. Format
            await _formattingService.FormatFilesAsync(
                byFile.Select(g => g.Key).ToList(),
                ct);

            // 7. Validate compilation
            var diagnostics = await _diagnosticService.GetErrorsAsync(ct);
            if (diagnostics.Any())
            {
                LogCompilationErrors(diagnostics.Count);

                if (mode == ApplyMode.AllOrNothing)
                {
                    await _snapshotService.RollbackAsync(snapshotId, ct);
                    return ReplaceResult.CompilationFailed(diagnostics, appliedChanges);
                }
            }

            // 8. Git commit
            if (commitMessage != null && _gitService != null)
            {
                await _gitService.CommitAsync(commitMessage, ct);
            }

            LogApplyCompleted(appliedChanges.Count, failedChanges.Count);

            return ReplaceResult.Success(
                appliedChanges.Count,
                failedChanges.Count,
                appliedChanges,
                failedChanges);
        }
        catch (Exception ex)
        {
            LogApplyFailed(ex);

            await _snapshotService.RollbackAsync(snapshotId, ct);
            throw;
        }
    }

    private async Task<FileReplaceResult> ApplyFileReplacementsAsync(
        string filePath,
        List<CodeReplacement> replacements,
        CancellationToken ct)
    {
        var applied = new List<AppliedChange>();
        var failed = new List<FailedChange>();

        // Сортируем по позиции (от конца к началу, чтобы не сбивать offsets)
        var sortedReplacements = replacements
            .Select(r => (Replacement: r, Match: _matchRegistry[r.MatchId]))
            .OrderByDescending(x => x.Match.ContainerStartLine)
            .ToList();

        var document = _solutionManager.GetDocumentByPath(filePath);
        var text = await document!.GetTextAsync(ct);
        var newText = text;

        foreach (var (replacement, match) in sortedReplacements)
        {
            try
            {
                // Найти span контейнера в текущем тексте
                var syntaxTree = await document.GetSyntaxTreeAsync(ct);
                var root = await syntaxTree!.GetRootAsync(ct);

                // Найти узел по FQN или позиции
                var containerNode = FindContainerNode(root, match);
                if (containerNode == null)
                {
                    failed.Add(new FailedChange
                    {
                        MatchId = replacement.MatchId,
                        FilePath = filePath,
                        Error = "Container node not found (code may have changed)"
                    });
                    continue;
                }

                // Парсим новый код
                var newCode = replacement.NewCode;
                var newNode = SyntaxFactory.ParseMemberDeclaration(newCode)
                    ?? SyntaxFactory.ParseStatement(newCode) as SyntaxNode
                    ?? throw new InvalidOperationException("Failed to parse replacement code");

                // Заменяем
                var newRoot = root.ReplaceNode(containerNode, newNode);
                document = document.WithSyntaxRoot(newRoot);

                applied.Add(new AppliedChange
                {
                    MatchId = replacement.MatchId,
                    FilePath = filePath,
                    OldCode = match.FullCode,
                    NewCode = newCode,
                    Description = replacement.Description
                });
            }
            catch (Exception ex)
            {
                failed.Add(new FailedChange
                {
                    MatchId = replacement.MatchId,
                    FilePath = filePath,
                    Error = ex.Message
                });
            }
        }

        // Сохраняем документ
        if (applied.Any())
        {
            await _solutionManager.ApplyDocumentChangesAsync(document, ct);
        }

        return new FileReplaceResult(applied, failed);
    }

    private List<ReplaceConflict> DetectConflicts(IReadOnlyList<CodeReplacement> replacements)
    {
        var conflicts = new List<ReplaceConflict>();

        // Группируем по файлу и контейнеру
        var byContainer = replacements
            .Select(r => (Replacement: r, Match: _matchRegistry[r.MatchId]))
            .GroupBy(x => (x.Match.FilePath, x.Match.ContainerFqn))
            .Where(g => g.Count() > 1);

        foreach (var group in byContainer)
        {
            conflicts.Add(new ReplaceConflict
            {
                MatchIds = group.Select(x => x.Replacement.MatchId).ToList(),
                FilePath = group.Key.FilePath,
                ContainerFqn = group.Key.ContainerFqn,
                Message = "Multiple replacements target the same container"
            });
        }

        return conflicts;
    }
}
```

---

### Phase 5: MCP Tools (1 день)

**Цель**: Создать MCP endpoint.

#### SemanticReplaceTools.cs

```csharp
namespace UltrasharpTools.Tools.Mcp.Tools;

public sealed partial class SemanticReplaceTools
{
    private readonly ISemanticReplaceService _replaceService;
    private readonly ILogger<SemanticReplaceTools> _logger;

    public SemanticReplaceTools(
        ISemanticReplaceService replaceService,
        ILogger<SemanticReplaceTools> logger)
    {
        _replaceService = replaceService;
        _logger = logger;
    }

    [DroidTool("semantic_replace")]
    [Description(@"
Batch find and replace with full context extraction.

PREVIEW MODE (default):
- pattern: regex, FQN, or natural language query
- searchMode: 'regex' | 'roslyn' | 'semantic'
- scope: 'statement' | 'block' | 'member' | 'type' | 'file'
- Returns list of matches with full container code

APPLY MODE:
- replacements: [{matchId, newCode}] for manual changes
- OR transformation: natural language description for LLM-based changes
- useSemanticModel: true to enable LLM transformation

Examples:
1. Preview Console.WriteLine usages:
   semantic_replace(pattern: 'Console\\.WriteLine', scope: 'member')

2. Manual replace:
   semantic_replace(apply: true, replacements: [{matchId: 'sr-xxx', newCode: '...'}])

3. Semantic transform:
   semantic_replace(pattern: 'Console.WriteLine', transformation: 'Replace with ILogger', useSemanticModel: true, apply: true)
")]
    public async Task<string> SemanticReplaceAsync(
        [Description("Pattern to search (regex, FQN, or natural language)")]
        string pattern,

        [Description("Search mode: regex, roslyn, semantic")]
        string searchMode = "regex",

        [Description("Context scope: statement, block, member, type, file")]
        string scope = "member",

        [Description("File pattern filter (glob)")]
        string? filePattern = null,

        [Description("Namespace filter")]
        string? namespaceFilter = null,

        [Description("Max results")]
        int limit = 100,

        [Description("Batch replacements [{matchId, newCode}]")]
        string? replacements = null,

        [Description("Transformation description for LLM")]
        string? transformation = null,

        [Description("Use LLM for transformation")]
        bool useSemanticModel = false,

        [Description("Apply changes (false = preview only)")]
        bool apply = false,

        [Description("Apply mode: AllOrNothing, BestEffort")]
        string applyMode = "AllOrNothing",

        [Description("Commit message")]
        string? commitMessage = null,

        CancellationToken ct = default)
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                var searchModeEnum = Enum.Parse<SearchMode>(searchMode, ignoreCase: true);
                var scopeEnum = Enum.Parse<ReplaceScope>(scope, ignoreCase: true);
                var applyModeEnum = Enum.Parse<ApplyMode>(applyMode, ignoreCase: true);

                // APPLY MODE
                if (apply)
                {
                    // Manual replacements
                    if (!string.IsNullOrEmpty(replacements))
                    {
                        var replacementList = JsonSerializer.Deserialize<List<CodeReplacement>>(replacements)
                            ?? throw new ArgumentException("Invalid replacements JSON");

                        var result = await _replaceService.ApplyAsync(
                            replacementList,
                            applyModeEnum,
                            commitMessage,
                            ct);

                        return FormatApplyResult(result);
                    }

                    // Semantic transformation
                    if (!string.IsNullOrEmpty(transformation) && useSemanticModel)
                    {
                        var result = await _replaceService.TransformAsync(
                            pattern,
                            searchModeEnum,
                            scopeEnum,
                            transformation,
                            filePattern,
                            namespaceFilter,
                            commitMessage,
                            ct);

                        return FormatApplyResult(result);
                    }

                    throw new ArgumentException("For apply mode, provide either 'replacements' or 'transformation' with 'useSemanticModel: true'");
                }

                // PREVIEW MODE
                var preview = await _replaceService.PreviewAsync(
                    pattern,
                    searchModeEnum,
                    scopeEnum,
                    filePattern,
                    namespaceFilter,
                    limit,
                    offset: 0,
                    ct);

                return FormatPreviewResult(preview);
            },
            "semantic_replace",
            _logger);
    }

    private static string FormatPreviewResult(SemanticReplacePreviewResult preview)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Found {preview.TotalMatches} matches:");
        sb.AppendLine();

        foreach (var match in preview.Matches)
        {
            sb.AppendLine($"### {match.Id}");
            sb.AppendLine($"**FQN**: `{match.ContainerFqn}`");
            sb.AppendLine($"**File**: {match.FilePath}:{match.MatchLine}");
            sb.AppendLine($"**Match**: `{match.MatchFragment}`");

            if (match.SemanticSimilarity.HasValue)
            {
                sb.AppendLine($"**Similarity**: {match.SemanticSimilarity:P0}");
            }

            sb.AppendLine();
            sb.AppendLine("```csharp");
            sb.AppendLine(match.FullCode);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        if (preview.HasMore)
        {
            sb.AppendLine($"*Showing {preview.Matches.Count} of {preview.TotalMatches}. Use offset/limit for pagination.*");
        }

        return sb.ToString();
    }

    private static string FormatApplyResult(ReplaceResult result)
    {
        var sb = new StringBuilder();

        if (result.Success)
        {
            sb.AppendLine($"✅ Successfully applied {result.Applied} changes");
        }
        else
        {
            sb.AppendLine($"❌ Failed: {result.Error}");
        }

        if (result.Failed > 0)
        {
            sb.AppendLine($"⚠️ {result.Failed} changes failed");
        }

        sb.AppendLine();

        foreach (var change in result.Changes.Take(10))
        {
            sb.AppendLine($"- ✅ {change.MatchId}: {change.FilePath}");
        }

        foreach (var failure in result.Failures.Take(10))
        {
            sb.AppendLine($"- ❌ {failure.MatchId}: {failure.Error}");
        }

        return sb.ToString();
    }
}
```

---

### Phase 6: Semantic Transformer (опционально, 2 дня)

**Цель**: LLM-based трансформация.

#### SemanticTransformerService.cs

```csharp
namespace UltrasharpTools.Tools.Replace.Services;

/// <summary>
/// LLM-based code transformation.
/// Requires external LLM API (Ollama, OpenAI, Anthropic).
/// </summary>
public sealed partial class SemanticTransformerService : ISemanticTransformerService
{
    private readonly ILlmClient? _llmClient;
    private readonly ILogger<SemanticTransformerService> _logger;

    public bool IsAvailable => _llmClient != null;

    public async Task<CodeReplacement> TransformAsync(
        CodeMatch match,
        string transformation,
        CancellationToken ct)
    {
        if (_llmClient == null)
            throw new InvalidOperationException("LLM client is not configured");

        var prompt = BuildTransformationPrompt(match, transformation);

        var response = await _llmClient.CompleteAsync(prompt, ct);

        var newCode = ExtractCodeFromResponse(response);

        // Validate syntax
        var syntaxTree = CSharpSyntaxTree.ParseText(newCode);
        var diagnostics = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (diagnostics.Any())
        {
            throw new InvalidOperationException(
                $"LLM generated invalid code: {string.Join(", ", diagnostics.Select(d => d.GetMessage()))}");
        }

        return new CodeReplacement
        {
            MatchId = match.Id,
            NewCode = newCode,
            Description = $"LLM transformation: {transformation}"
        };
    }

    private static string BuildTransformationPrompt(CodeMatch match, string transformation)
    {
        return $"""
            You are a code transformation assistant. Transform the following C# code according to the instructions.

            ## Instructions
            {transformation}

            ## Original Code
            ```csharp
            {match.FullCode}
            ```

            ## Context
            - Container: {match.ContainerFqn}
            - Match location: line {match.MatchLine}
            - Match fragment: {match.MatchFragment}

            ## Requirements
            - Return ONLY the transformed code, no explanations
            - Preserve the overall structure and formatting style
            - Ensure the code compiles
            - Apply the transformation to ALL relevant places in the code

            ## Transformed Code
            ```csharp
            """;
    }

    private static string ExtractCodeFromResponse(string response)
    {
        // Extract code from markdown code block
        var match = Regex.Match(response, @"```csharp\s*([\s\S]*?)\s*```");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // If no code block, assume entire response is code
        return response.Trim();
    }
}
```

---

## Регистрация в DI

```csharp
// ServiceCollectionExtensions.cs

public static IServiceCollection WithSemanticReplace(this IServiceCollection services)
{
    services.AddSingleton<IPatternMatcherService, PatternMatcherService>();
    services.AddSingleton<IContextExtractorService, ContextExtractorService>();
    services.AddSingleton<IBatchReplacerService, BatchReplacerService>();
    services.AddSingleton<ISemanticReplaceService, SemanticReplaceService>();

    // Optional: LLM transformer
    services.AddSingleton<ISemanticTransformerService, SemanticTransformerService>();

    return services;
}
```

---

## Тестирование

### Unit Tests

```csharp
[TestClass]
public class PatternMatcherServiceTests
{
    [TestMethod]
    public async Task FindRegexMatches_ConsoleWriteLine_ReturnsAllOccurrences()
    {
        // Arrange
        var service = CreateService();
        await LoadTestSolution();

        // Act
        var matches = await service.FindMatchesAsync(
            @"Console\.WriteLine\(",
            SearchMode.Regex,
            filePattern: null,
            namespaceFilter: null,
            limit: 100,
            CancellationToken.None);

        // Assert
        Assert.IsTrue(matches.Count > 0);
        Assert.IsTrue(matches.All(m => m.MatchedText.Contains("Console.WriteLine")));
    }
}

[TestClass]
public class BatchReplacerServiceTests
{
    [TestMethod]
    public async Task ApplyReplacements_ValidChanges_AppliesAll()
    {
        // Arrange
        var service = CreateService();
        var replacements = new List<CodeReplacement>
        {
            new() { MatchId = "sr-001", NewCode = "// replaced" }
        };

        // Act
        var result = await service.ApplyReplacementsAsync(
            replacements,
            ApplyMode.AllOrNothing,
            commitMessage: null,
            CancellationToken.None);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.Applied);
    }

    [TestMethod]
    public async Task ApplyReplacements_InvalidCode_RollsBack()
    {
        // Arrange
        var service = CreateService();
        var replacements = new List<CodeReplacement>
        {
            new() { MatchId = "sr-001", NewCode = "invalid { code" }
        };

        // Act
        var result = await service.ApplyReplacementsAsync(
            replacements,
            ApplyMode.AllOrNothing,
            commitMessage: null,
            CancellationToken.None);

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, result.Applied);
    }
}
```

### Integration Tests

```csharp
[TestClass]
public class SemanticReplaceIntegrationTests
{
    [TestMethod]
    public async Task EndToEnd_PreviewAndApply_Success()
    {
        // 1. Preview
        var preview = await _service.PreviewAsync(
            pattern: "Console.WriteLine",
            searchMode: SearchMode.Regex,
            scope: ReplaceScope.Member,
            ct: CancellationToken.None);

        Assert.IsTrue(preview.Matches.Any());

        // 2. Transform first match
        var replacement = new CodeReplacement
        {
            MatchId = preview.Matches[0].Id,
            NewCode = preview.Matches[0].FullCode.Replace(
                "Console.WriteLine",
                "_logger.LogInformation")
        };

        // 3. Apply
        var result = await _service.ApplyAsync(
            [replacement],
            ApplyMode.AllOrNothing,
            commitMessage: "test",
            CancellationToken.None);

        Assert.IsTrue(result.Success);
    }
}
```

---

## Roadmap

| Phase | Описание | Срок | Статус |
|-------|----------|------|--------|
| 1 | Models & Interfaces | 1 день | 🔲 |
| 2 | Pattern Matching | 2 дня | 🔲 |
| 3 | Context Extraction | 2 дня | 🔲 |
| 4 | Batch Replacer | 2 дня | 🔲 |
| 5 | MCP Tools | 1 день | 🔲 |
| 6 | Semantic Transformer | 2 дня | 🔲 (optional) |
| 7 | Testing | 2 дня | 🔲 |

**Итого**: 10-12 дней (8 дней без Semantic Transformer)
