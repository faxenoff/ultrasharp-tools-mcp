using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class CodeModificationService
{
    // Member operations (3000-3009)
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information,
        Message = "Adding member to type {TypeName} in document {DocumentPath}")]
    private partial void LogAddingMember(string typeName, string? documentPath);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information,
        Message = "Adding statement to method {MethodName} in document {DocumentPath}")]
    private partial void LogAddingStatement(string methodName, string? documentPath);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information,
        Message = "Replacing node in document {DocumentPath}")]
    private partial void LogReplacingNode(string? documentPath);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information,
        Message = "Detected deletion operation for node {NodeKind}")]
    private partial void LogDeletionOperation(string nodeKind);

    // Symbol operations (3010-3019)
    [LoggerMessage(EventId = 3010, Level = LogLevel.Information,
        Message = "Renaming symbol {SymbolName} to {NewName}")]
    private partial void LogRenamingSymbol(string symbolName, string newName);

    [LoggerMessage(EventId = 3011, Level = LogLevel.Information,
        Message = "Replacing all references to symbol {SymbolName} with text '{ReplacementText}'")]
    private partial void LogReplacingReferences(string symbolName, string replacementText);

    [LoggerMessage(EventId = 3012, Level = LogLevel.Debug,
        Message = "Skipping replacement for node at {Location} due to filter predicate")]
    private partial void LogSkippingReplacement(string location);

    [LoggerMessage(EventId = 3013, Level = LogLevel.Information,
        Message = "Target is a valid symbol: {SymbolName}")]
    private partial void LogValidSymbol(string symbolName);

    [LoggerMessage(EventId = 3014, Level = LogLevel.Information,
        Message = "Symbol replaced in {DocumentPath}")]
    private partial void LogSymbolReplaced(string? documentPath);

    // Find and replace operations (3020-3029)
    [LoggerMessage(EventId = 3020, Level = LogLevel.Information,
        Message = "Performing find and replace with regex '{RegexPattern}' on target '{TargetString}'")]
    private partial void LogFindAndReplace(string regexPattern, string targetString);

    [LoggerMessage(EventId = 3021, Level = LogLevel.Information,
        Message = "Target string is not a valid symbol: {Error}")]
    private partial void LogNotValidSymbol(string error);

    [LoggerMessage(EventId = 3022, Level = LogLevel.Information,
        Message = "Treating '{Target}' as a file path pattern")]
    private partial void LogFilePathPattern(string target);

    [LoggerMessage(EventId = 3023, Level = LogLevel.Information,
        Message = "Document matched pattern: {DocumentPath}")]
    private partial void LogDocumentMatched(string? documentPath);

    [LoggerMessage(EventId = 3024, Level = LogLevel.Information,
        Message = "Found {Count} documents matching pattern '{Pattern}'")]
    private partial void LogDocumentsFound(int count, string pattern);

    [LoggerMessage(EventId = 3025, Level = LogLevel.Information,
        Message = "Processed {TotalDocuments} documents, {ChangedDocuments} changed")]
    private partial void LogDocumentsProcessed(int totalDocuments, int changedDocuments);

    // Formatting operations (3030-3039)
    [LoggerMessage(EventId = 3030, Level = LogLevel.Debug,
        Message = "Formatting document: {DocumentPath}")]
    private partial void LogFormattingDocument(string? documentPath);

    [LoggerMessage(EventId = 3031, Level = LogLevel.Debug,
        Message = "Partial formatting span [{Start}..{End}] in {DocumentPath}")]
    private partial void LogPartialFormatting(int start, int end, string? documentPath);

    [LoggerMessage(EventId = 3032, Level = LogLevel.Debug,
        Message = "Document partially formatted: {DocumentPath}")]
    private partial void LogDocumentPartiallyFormatted(string? documentPath);

    [LoggerMessage(EventId = 3033, Level = LogLevel.Debug,
        Message = "Skipping formatting (file too small): {DocumentPath}")]
    private partial void LogSkippingFormattingSmallFile(string? documentPath);

    [LoggerMessage(EventId = 3034, Level = LogLevel.Debug,
        Message = "Document fully formatted: {DocumentPath}")]
    private partial void LogDocumentFullyFormatted(string? documentPath);

    [LoggerMessage(EventId = 3035, Level = LogLevel.Debug,
        Message = "Pre-apply formatting for changed document: {DocumentPath}")]
    private partial void LogPreApplyFormattingChanged(string? documentPath);

    [LoggerMessage(EventId = 3036, Level = LogLevel.Debug,
        Message = "Pre-apply formatting for added document: {DocumentPath}")]
    private partial void LogPreApplyFormattingAdded(string? documentPath);

    // Apply changes operations (3040-3049)
    [LoggerMessage(EventId = 3040, Level = LogLevel.Error,
        Message = "Cannot apply changes: Workspace is not an MSBuildWorkspace or is null.")]
    private partial void LogCannotApplyWorkspaceNull();

    [LoggerMessage(EventId = 3041, Level = LogLevel.Information,
        Message = "Added new document for git tracking: {DocumentPath}")]
    private partial void LogAddedDocumentForGit(string? documentPath);

    [LoggerMessage(EventId = 3042, Level = LogLevel.Information,
        Message = "Marked removed document for git tracking: {DocumentPath}")]
    private partial void LogRemovedDocumentForGit(string? documentPath);

    [LoggerMessage(EventId = 3043, Level = LogLevel.Information,
        Message = "Applying changes to workspace for {DocumentCount} changed documents across {ProjectCount} projects.")]
    private partial void LogApplyingChanges(int documentCount, int projectCount);

    [LoggerMessage(EventId = 3044, Level = LogLevel.Information,
        Message = "Changes applied successfully to the workspace.")]
    private partial void LogChangesApplied();

    [LoggerMessage(EventId = 3045, Level = LogLevel.Error,
        Message = "Failed to apply changes to the workspace.")]
    private partial void LogChangesApplyFailed();

    // Quick lint operations (3050-3059)
    [LoggerMessage(EventId = 3050, Level = LogLevel.Information,
        Message = "Quick lint check: solutionPath='{SolutionPath}', changedFiles={Count}, csFiles={CsCount}")]
    private partial void LogQuickLintCheck(string? solutionPath, int count, int csCount);

    [LoggerMessage(EventId = 3051, Level = LogLevel.Information,
        Message = "Running quick lint on {Count} modified C# files")]
    private partial void LogRunningQuickLint(int count);

    [LoggerMessage(EventId = 3052, Level = LogLevel.Information,
        Message = "Quick lint completed: {Errors} errors, {Warnings} warnings")]
    private partial void LogQuickLintCompleted(int errors, int warnings);

    [LoggerMessage(EventId = 3053, Level = LogLevel.Warning,
        Message = "Quick lint skipped: csFiles.Any()={HasCsFiles}, solutionPath isEmpty={IsEmpty}")]
    private partial void LogQuickLintSkipped(bool hasCsFiles, bool isEmpty);

    // Git operations (3060-3079)
    [LoggerMessage(EventId = 3060, Level = LogLevel.Debug,
        Message = "Solution is not in a Git repository, skipping Git operations")]
    private partial void LogNotInGitRepo();

    [LoggerMessage(EventId = 3061, Level = LogLevel.Debug,
        Message = "Solution is in a Git repository, processing Git operations")]
    private partial void LogInGitRepo();

    [LoggerMessage(EventId = 3062, Level = LogLevel.Information,
        Message = "Not on a SharpTools branch, creating one")]
    private partial void LogCreatingSharpToolsBranch();

    [LoggerMessage(EventId = 3063, Level = LogLevel.Information,
        Message = "Git operations completed successfully with commit message: {CommitMessage}")]
    private partial void LogGitOperationsCompleted(string commitMessage);

    [LoggerMessage(EventId = 3064, Level = LogLevel.Warning,
        Message = "Git operations failed but code changes were still applied")]
    private partial void LogGitOperationsFailed(Exception exception);

    // Undo operations (3070-3079)
    [LoggerMessage(EventId = 3070, Level = LogLevel.Error,
        Message = "Cannot undo changes: Workspace is not an MSBuildWorkspace or is null.")]
    private partial void LogCannotUndoWorkspaceNull();

    [LoggerMessage(EventId = 3071, Level = LogLevel.Error,
        Message = "Cannot undo changes: Current solution or its file path is null.")]
    private partial void LogCannotUndoSolutionNull();

    [LoggerMessage(EventId = 3072, Level = LogLevel.Error,
        Message = "Cannot undo changes: Solution is not in a Git repository.")]
    private partial void LogCannotUndoNotGitRepo();

    [LoggerMessage(EventId = 3073, Level = LogLevel.Error,
        Message = "Cannot undo changes: Not on a SharpTools branch.")]
    private partial void LogCannotUndoNotSharpToolsBranch();

    [LoggerMessage(EventId = 3074, Level = LogLevel.Information,
        Message = "Attempting to undo last change by reverting last Git commit.")]
    private partial void LogAttemptingUndo();

    [LoggerMessage(EventId = 3075, Level = LogLevel.Error,
        Message = "Git revert operation failed.")]
    private partial void LogRevertFailed();

    [LoggerMessage(EventId = 3076, Level = LogLevel.Information,
        Message = "Successfully reverted the last change using Git.")]
    private partial void LogRevertSucceeded();
}
