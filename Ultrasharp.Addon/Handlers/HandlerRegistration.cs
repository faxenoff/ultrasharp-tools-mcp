using Ultrasharp.Addon.Ipc;

namespace Ultrasharp.Addon.Handlers;

/// <summary>
/// Registers all request handlers with the RequestRouter via DI.
/// </summary>
public static class HandlerRegistration
{
    public static void RegisterHandlers(IServiceCollection services)
    {
        services.AddSingleton<ParseHandler>();
        services.AddSingleton<StatusHandler>();
        services.AddSingleton<DocumentHandler>();
        services.AddSingleton<EnrichHandler>();
        services.AddSingleton<AnalysisHandler>();
        services.AddSingleton<ValidationHandler>();
        services.AddSingleton<ModificationHandler>();

        // Wire handlers into router after build
        services.AddSingleton<IHandlerInitializer, HandlerInitializer>();
    }
}

public interface IHandlerInitializer
{
    void Initialize();
}

/// <summary>
/// Wires all handlers into the RequestRouter at startup.
/// Called from AddonLifecycleService.
/// </summary>
public sealed class HandlerInitializer : IHandlerInitializer
{
    private readonly RequestRouter _router;
    private readonly ParseHandler _parse;
    private readonly StatusHandler _status;
    private readonly DocumentHandler _document;
    private readonly EnrichHandler _enrich;
    private readonly AnalysisHandler _analysis;
    private readonly ValidationHandler _validation;
    private readonly ModificationHandler _modification;

    public HandlerInitializer(
        RequestRouter router,
        ParseHandler parse,
        StatusHandler status,
        DocumentHandler document,
        EnrichHandler enrich,
        AnalysisHandler analysis,
        ValidationHandler validation,
        ModificationHandler modification)
    {
        _router = router;
        _parse = parse;
        _status = status;
        _document = document;
        _enrich = enrich;
        _analysis = analysis;
        _validation = validation;
        _modification = modification;
    }

    public void Initialize()
    {
        // Phase 1 handlers (syntax only, no solution required)
        _router.Register("parse", _parse.HandleParseAsync);
        _router.Register("parseBatch", _parse.HandleParseBatchAsync);

        // Status
        _router.Register("status", _status.HandleStatusAsync);
        _router.Register("loadSolution", _status.HandleLoadSolutionAsync);
        _router.Register("unloadSolution", _status.HandleUnloadSolutionAsync);

        // Document management (incremental updates)
        _router.Register("updateDocument", _document.HandleUpdateDocumentAsync);
        _router.Register("addDocument", _document.HandleAddDocumentAsync);
        _router.Register("removeDocument", _document.HandleRemoveDocumentAsync);

        // Phase 2 handlers (require loaded solution)
        _router.Register("enrich", _enrich.HandleEnrichAsync);
        _router.Register("enrichBatch", _enrich.HandleEnrichBatchAsync);

        _router.Register("findReferences", _analysis.HandleFindReferencesAsync);
        _router.Register("getDefinition", _analysis.HandleGetDefinitionAsync);
        _router.Register("getCallGraph", _analysis.HandleGetCallGraphAsync);
        _router.Register("getImplementations", _analysis.HandleGetImplementationsAsync);
        _router.Register("traceFlow", _analysis.HandleTraceFlowAsync);
        _router.Register("traceBackwards", _analysis.HandleTraceBackwardsAsync);

        _router.Register("validate", _validation.HandleValidateAsync);

        _router.Register("modifyCode", _modification.HandleModifyCodeAsync);
        _router.Register("renameSymbol", _modification.HandleRenameSymbolAsync);
        _router.Register("applyCodeFix", _modification.HandleApplyCodeFixAsync);
        _router.Register("formatCode", _modification.HandleFormatCodeAsync);
    }
}
