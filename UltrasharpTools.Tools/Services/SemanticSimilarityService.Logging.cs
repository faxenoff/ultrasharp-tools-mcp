using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class SemanticSimilarityService
{
    [LoggerMessage(EventId = 3900, Level = LogLevel.Information,
        Message = "Starting semantic similarity analysis with threshold {Threshold}, MaxDOP: {MaxDop}")]
    private partial void LogStartingMethodAnalysis(double threshold, int maxDop);

    [LoggerMessage(EventId = 3901, Level = LogLevel.Information,
        Message = "Semantic similarity analysis cancelled during project iteration for {ProjectName}.")]
    private partial void LogMethodAnalysisCancelledForProject(string projectName);

    [LoggerMessage(EventId = 3902, Level = LogLevel.Debug,
        Message = "Analyzing project: {ProjectName}")]
    private partial void LogAnalyzingProject(string projectName);

    [LoggerMessage(EventId = 3903, Level = LogLevel.Warning,
        Message = "Could not get compilation for project {ProjectName}")]
    private partial void LogCompilationFailed(string projectName);

    [LoggerMessage(EventId = 3904, Level = LogLevel.Trace,
        Message = "Analyzing document: {DocumentFilePath}")]
    private partial void LogAnalyzingDocument(string? documentFilePath);

    [LoggerMessage(EventId = 3905, Level = LogLevel.Information,
        Message = "Feature extraction cancelled for method {MethodName} in {FilePath}")]
    private partial void LogMethodExtractionCancelled(string methodName, string? filePath);

    [LoggerMessage(EventId = 3906, Level = LogLevel.Warning,
        Message = "Failed to extract features for method {MethodName} in {FilePath}")]
    private partial void LogMethodExtractionFailed(Exception exception, string methodName, string? filePath);

    [LoggerMessage(EventId = 3907, Level = LogLevel.Information,
        Message = "Semantic similarity analysis was cancelled before comparison.")]
    private partial void LogMethodAnalysisCancelledBeforeComparison();

    [LoggerMessage(EventId = 3908, Level = LogLevel.Information,
        Message = "Extracted features for {MethodCount} methods. Starting similarity comparison.")]
    private partial void LogMethodFeaturesExtracted(int methodCount);

    [LoggerMessage(EventId = 3909, Level = LogLevel.Debug,
        Message = "Method {MethodName} in {FilePath} has {LineCount} lines, which is less than the filter of {FilterCount}. Skipping.")]
    private partial void LogMethodTooShort(string methodName, string filePath, int lineCount, int filterCount);

    [LoggerMessage(EventId = 3910, Level = LogLevel.Debug,
        Message = "Calculated complexity for {Fqn}: {CyclomaticComplexity}")]
    private partial void LogMethodComplexity(string fqn, int cyclomaticComplexity);

    [LoggerMessage(EventId = 3911, Level = LogLevel.Warning,
        Message = "Failed to calculate complexity for {Fqn}")]
    private partial void LogComplexityCalculationFailed(Exception exception, string fqn);

    [LoggerMessage(EventId = 3912, Level = LogLevel.Information,
        Message = "Extracted {Features} features from {Fqn}: CallGraph={CallGraphCount}, TypeRefs={TypeRefCount}, LineCount={LineCount}")]
    private partial void LogFeaturesExtracted(int features, string fqn, int callGraphCount, int typeRefCount, int lineCount);

    [LoggerMessage(EventId = 3913, Level = LogLevel.Debug,
        Message = "Comparing {MethodCount} methods pairwise (threshold: {Threshold})")]
    private partial void LogStartingComparison(int methodCount, double threshold);

    [LoggerMessage(EventId = 3914, Level = LogLevel.Debug,
        Message = "Similarity between {Method1} and {Method2}: {Similarity:F3}")]
    private partial void LogSimilarityCalculated(string method1, string method2, double similarity);

    [LoggerMessage(EventId = 3915, Level = LogLevel.Information,
        Message = "Found {ResultCount} similar method pairs above threshold {Threshold}")]
    private partial void LogSimilarMethodsFound(int resultCount, double threshold);

    [LoggerMessage(EventId = 3916, Level = LogLevel.Information,
        Message = "Starting class semantic similarity analysis with threshold {Threshold}, MaxDOP: {MaxDop}")]
    private partial void LogStartingClassAnalysis(double threshold, int maxDop);

    [LoggerMessage(EventId = 3917, Level = LogLevel.Information,
        Message = "Semantic similarity analysis cancelled during project iteration for {ProjectName}.")]
    private partial void LogClassAnalysisCancelledForProject(string projectName);

    [LoggerMessage(EventId = 3918, Level = LogLevel.Debug,
        Message = "Analyzing project for classes: {ProjectName}")]
    private partial void LogAnalyzingProjectForClasses(string projectName);

    [LoggerMessage(EventId = 3919, Level = LogLevel.Warning,
        Message = "Could not get compilation for project {ProjectName}")]
    private partial void LogClassCompilationFailed(string projectName);

    [LoggerMessage(EventId = 3920, Level = LogLevel.Trace,
        Message = "Analyzing document for classes: {DocumentFilePath}")]
    private partial void LogAnalyzingDocumentForClasses(string? documentFilePath);

    [LoggerMessage(EventId = 3921, Level = LogLevel.Information,
        Message = "Feature extraction cancelled for class {ClassName} in {FilePath}")]
    private partial void LogClassExtractionCancelled(string className, string? filePath);

    [LoggerMessage(EventId = 3922, Level = LogLevel.Warning,
        Message = "Failed to extract features for class {ClassName} in {FilePath}")]
    private partial void LogClassExtractionFailed(Exception exception, string className, string? filePath);

    [LoggerMessage(EventId = 3923, Level = LogLevel.Information,
        Message = "Semantic similarity analysis for classes was cancelled before comparison.")]
    private partial void LogClassAnalysisCancelledBeforeComparison();

    [LoggerMessage(EventId = 3924, Level = LogLevel.Information,
        Message = "Extracted features for {ClassCount} classes. Starting similarity comparison.")]
    private partial void LogClassFeaturesExtracted(int classCount);

    [LoggerMessage(EventId = 3925, Level = LogLevel.Debug,
        Message = "Class {ClassName} in {FilePath} has {LineCount} lines, which is less than the filter of {FilterCount}. Skipping.")]
    private partial void LogClassTooShort(string className, string filePath, int lineCount, int filterCount);

    [LoggerMessage(EventId = 3926, Level = LogLevel.Information,
        Message = "Found {ResultCount} similar class pairs above threshold {Threshold}")]
    private partial void LogSimilarClassesFound(int resultCount, double threshold);

    [LoggerMessage(EventId = 3927, Level = LogLevel.Information,
        Message = "Comparing {A} vs {B}: Jaccard={Jaccard:F3}, Struct={Struct:F3}, Cosine={Cosine:F3} → Combined={Combined:F3}")]
    private partial void LogDetailedComparison(string a, string b, double jaccard, double @struct, double cosine, double combined);

    [LoggerMessage(EventId = 3928, Level = LogLevel.Debug,
        Message = "ControlFlowGraph created for method {MethodName} in {FilePath}. BasicBlockCount: {BasicBlockCount}")]
    private partial void LogCfgCreated(string methodName, string? filePath, int basicBlockCount);

    [LoggerMessage(EventId = 3929, Level = LogLevel.Warning,
        Message = "Failed to create ControlFlowGraph for method {MethodName} in {FilePath}. CFG-based features will be zero.")]
    private partial void LogCfgCreationFailed(Exception exception, string methodName, string? filePath);

    [LoggerMessage(EventId = 3930, Level = LogLevel.Information,
        Message = "Starting parallel similarity comparison for {MethodCount} methods.")]
    private partial void LogStartingParallelMethodComparison(int methodCount);

    [LoggerMessage(EventId = 3931, Level = LogLevel.Debug,
        Message = "Comparing method {MethodName} ({Fqn}) with other methods (parallel mode).")]
    private partial void LogComparingMethod(string methodName, string fqn);

    [LoggerMessage(EventId = 3932, Level = LogLevel.Debug,
        Message = "Skipping comparison between overloads: {Method1Fqn} ({Params1}) and {Method2Fqn} ({Params2})")]
    private partial void LogSkippingOverloads(string method1Fqn, string params1, string method2Fqn, string params2);

    [LoggerMessage(EventId = 3933, Level = LogLevel.Debug,
        Message = "Method {OtherMethodName} ({OtherFqn}) is similar to {CurrentMethodName} ({CurrentFqn}) with score {SimilarityScore}")]
    private partial void LogMethodSimilarityFound(string otherMethodName, string otherFqn, string currentMethodName, string currentFqn, double similarityScore);

    [LoggerMessage(EventId = 3934, Level = LogLevel.Information,
        Message = "Found similarity group of {GroupSize} methods, starting with {MethodName} ({Fqn}), Avg Score: {Score:F2}")]
    private partial void LogSimilarityGroupFound(int groupSize, string methodName, string fqn, double score);

    [LoggerMessage(EventId = 3935, Level = LogLevel.Information,
        Message = "Parallel similarity analysis complete. Found {GroupCount} groups.")]
    private partial void LogParallelMethodAnalysisComplete(int groupCount);

    [LoggerMessage(EventId = 3936, Level = LogLevel.Information,
        Message = "Starting parallel class similarity comparison for {ClassCount} classes.")]
    private partial void LogStartingParallelClassComparison(int classCount);

    [LoggerMessage(EventId = 3937, Level = LogLevel.Information,
        Message = "Found class similarity group of {GroupSize}, starting with {ClassName}, Avg Score: {Score:F2}")]
    private partial void LogClassSimilarityGroupFound(int groupSize, string className, double score);

    [LoggerMessage(EventId = 3938, Level = LogLevel.Information,
        Message = "Parallel class similarity analysis complete. Found {GroupCount} groups.")]
    private partial void LogParallelClassAnalysisComplete(int groupCount);
}
