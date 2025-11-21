using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Service for analyzing log files with automatic format detection.
/// </summary>
public interface ILogAnalysisService
{
    /// <summary>
    /// Analyzes a log file and searches for entries matching criteria.
    /// </summary>
    /// <param name="filePath">Path to the log file</param>
    /// <param name="criteria">Search criteria</param>
    /// <param name="detailLevel">Detail level for output</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Log analysis result with matched entries</returns>
    Task<LogAnalysisResult> AnalyzeLogFileAsync(
        string filePath,
        LogSearchCriteria criteria,
        LogDetailLevel detailLevel = LogDetailLevel.Brief,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Detects the format of a log file by reading first few lines.
    /// </summary>
    /// <param name="filePath">Path to the log file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected log format</returns>
    Task<LogFormat> DetectLogFormatAsync(
        string filePath,
        CancellationToken cancellationToken = default
    );
}
