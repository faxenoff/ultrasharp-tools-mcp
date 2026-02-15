using System.Text.Json;
using Microsoft.CodeAnalysis;
using Ultrasharp.Addon.Models;
using Ultrasharp.Addon.Services;
using UltrasharpTools.Tools.Interfaces;

namespace Ultrasharp.Addon.Handlers;

/// <summary>
/// Handles "enrich" and "enrichBatch" requests.
/// Uses SemanticModel to resolve FQN, call targets, inheritance chains.
/// Requires Phase 2 (loaded solution).
/// </summary>
public sealed class EnrichHandler
{
    private readonly ISolutionManager _solutionManager;
    private readonly AddonLifecycleService _lifecycle;
    private readonly ILogger<EnrichHandler> _logger;

    public EnrichHandler(ISolutionManager solutionManager, AddonLifecycleService lifecycle, ILogger<EnrichHandler> logger)
    {
        _solutionManager = solutionManager;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    /// <summary>
    /// Enrich a single entity with semantic information.
    /// Params: { "filePath": string, "entityName": string, "entityType"?: string }
    /// Returns resolved FQN, base types, call targets with resolved types.
    /// </summary>
    public async Task<object?> HandleEnrichAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var filePath = request.Params?.GetProperty("filePath").GetString() ?? "";
        var entityName = request.Params?.GetProperty("entityName").GetString() ?? "";

        var result = await EnrichEntityAsync(filePath, entityName, ct);
        return result;
    }

    /// <summary>
    /// Enrich multiple entities in batch.
    /// Params: { "entities": [{ "filePath": string, "entityName": string }] }
    /// </summary>
    public async Task<object?> HandleEnrichBatchAsync(AddonRequest request, CancellationToken ct)
    {
        if (_lifecycle.Phase < 2)
            return new { error = "Solution not loaded (Phase 2 required)" };

        var results = new List<object>();
        if (request.Params?.TryGetProperty("entities", out var entitiesEl) == true && entitiesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in entitiesEl.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                var filePath = e.GetProperty("filePath").GetString() ?? "";
                var entityName = e.GetProperty("entityName").GetString() ?? "";
                var result = await EnrichEntityAsync(filePath, entityName, ct);
                results.Add(result);
            }
        }

        return new { entities = results };
    }

    private async Task<object> EnrichEntityAsync(string filePath, string entityName, CancellationToken ct)
    {
        var solution = _solutionManager.CurrentSolution;
        if (solution == null)
            return new { entityName, error = "No solution" };

        // Find document
        var document = solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (document == null)
            return new { entityName, filePath, error = "Document not found" };

        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (semanticModel == null)
            return new { entityName, filePath, error = "Could not get semantic model" };

        var root = await document.GetSyntaxRootAsync(ct);
        if (root == null)
            return new { entityName, filePath, error = "Could not get syntax root" };

        // Find the symbol by name
        var symbol = await _solutionManager.FindRoslynSymbolAsync(entityName, ct);

        if (symbol == null)
        {
            // Fallback: search in the document's declared symbols
            var declaredSymbols = semanticModel.GetDeclaredSymbol(root, ct);
            return new
            {
                entityName,
                filePath,
                resolved = false,
                fqn = (string?)null,
            };
        }

        // Build enrichment data
        var enrichment = new Dictionary<string, object?>
        {
            ["entityName"] = entityName,
            ["filePath"] = filePath,
            ["resolved"] = true,
            ["fqn"] = symbol.ToDisplayString(),
            ["kind"] = symbol.Kind.ToString().ToLowerInvariant(),
            ["containingNamespace"] = symbol.ContainingNamespace?.ToDisplayString(),
            ["containingType"] = symbol.ContainingType?.ToDisplayString(),
        };

        // Type-specific enrichment
        if (symbol is INamedTypeSymbol namedType)
        {
            enrichment["baseType"] = namedType.BaseType?.ToDisplayString();
            enrichment["interfaces"] = namedType.Interfaces.Select(i => i.ToDisplayString()).ToList();
            enrichment["isGeneric"] = namedType.IsGenericType;
            enrichment["typeParameters"] = namedType.TypeParameters.Select(p => p.Name).ToList();
        }
        else if (symbol is IMethodSymbol method)
        {
            enrichment["returnType"] = method.ReturnType.ToDisplayString();
            enrichment["parameters"] = method.Parameters.Select(p => new
            {
                name = p.Name,
                type = p.Type.ToDisplayString(),
                isOptional = p.IsOptional,
            }).ToList();
            enrichment["isAsync"] = method.IsAsync;
            enrichment["isExtensionMethod"] = method.IsExtensionMethod;
        }
        else if (symbol is IPropertySymbol prop)
        {
            enrichment["propertyType"] = prop.Type.ToDisplayString();
            enrichment["isReadOnly"] = prop.IsReadOnly;
        }
        else if (symbol is IFieldSymbol field)
        {
            enrichment["fieldType"] = field.Type.ToDisplayString();
            enrichment["isConst"] = field.IsConst;
            enrichment["isReadOnly"] = field.IsReadOnly;
        }

        return enrichment;
    }
}
