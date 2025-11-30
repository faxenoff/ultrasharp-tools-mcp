using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace UltrasharpTools.Tools.Mcp.Prompts;

/// <summary>
/// MCP Prompts for UltrasharpTools documentation.
/// Provides categorized documentation for all available tools.
/// </summary>
[McpServerPromptType]
public sealed class McpPrompts
{
    private static readonly string PromptsDirectory;

    static McpPrompts()
    {
        // Get the directory where the assembly is located
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? ".";

        // Try multiple locations for Prompts directory
        var candidates = new[]
        {
            Path.Combine(assemblyDir, "Prompts"),
            Path.Combine(assemblyDir, "..", "Prompts"),
            Path.Combine(assemblyDir, "..", "..", "Prompts"),
            Path.Combine(assemblyDir, "..", "..", "..", "Prompts"),
            Path.Combine(AppContext.BaseDirectory, "Prompts"),
        };

        PromptsDirectory = candidates.FirstOrDefault(Directory.Exists) ??
            Path.Combine(assemblyDir, "Prompts");
    }

    /// <summary>
    /// General overview of UltrasharpTools - key concepts, workflow, and tool categories.
    /// </summary>
    [McpServerPrompt(Name = "overview")]
    [Description("General overview of UltrasharpTools - key concepts, workflow, and tool categories.")]
    public static string Overview()
    {
        return LoadPrompt("overview.md");
    }

    /// <summary>
    /// Solution and project management tools - load_solution, load_project.
    /// </summary>
    [McpServerPrompt(Name = "solution")]
    [Description("Solution and project management tools - load_solution, load_project.")]
    public static string Solution()
    {
        return LoadPrompt("solution.md");
    }

    /// <summary>
    /// Code analysis and navigation tools - view_definition, get_members, find_references, etc.
    /// </summary>
    [McpServerPrompt(Name = "analysis")]
    [Description("Code analysis and navigation tools - view_definition, get_members, find_references, search_definitions, etc.")]
    public static string Analysis()
    {
        return LoadPrompt("analysis.md");
    }

    /// <summary>
    /// Code modification and refactoring tools - modify_code, add_member, rename_symbol, etc.
    /// </summary>
    [McpServerPrompt(Name = "modification")]
    [Description("Code modification and refactoring tools - modify_code, add_member, rename_symbol, find_and_replace, etc.")]
    public static string Modification()
    {
        return LoadPrompt("modification.md");
    }

    /// <summary>
    /// Code quality and formatting tools - format_code, analyze_code_style, apply_code_fixes.
    /// </summary>
    [McpServerPrompt(Name = "quality")]
    [Description("Code quality and formatting tools - format_code, analyze_code_style, apply_code_fixes, validate_file.")]
    public static string Quality()
    {
        return LoadPrompt("quality.md");
    }

    /// <summary>
    /// Execution tracing and debugging tools - trace_execution, trace_backwards, analyze_logs.
    /// </summary>
    [McpServerPrompt(Name = "tracing")]
    [Description("Execution tracing and debugging tools - trace_execution, trace_backwards, analyze_path_feasibility, analyze_logs.")]
    public static string Tracing()
    {
        return LoadPrompt("tracing.md");
    }

    /// <summary>
    /// Semantic search and AI-powered tools - semantic_search, pattern_search, detect_code_clones, semantic_merge.
    /// </summary>
    [McpServerPrompt(Name = "semantic")]
    [Description("Semantic search and AI-powered tools - semantic_search, pattern_search, detect_code_clones, semantic_merge. Requires semantic mode setup.")]
    public static string Semantic()
    {
        return LoadPrompt("semantic.md");
    }

    /// <summary>
    /// System capabilities and snapshot management - get_capabilities, create_snapshot, rollback_snapshot, undo.
    /// </summary>
    [McpServerPrompt(Name = "system")]
    [Description("System capabilities and snapshot management - get_capabilities, create_snapshot, list_snapshots, rollback_snapshot, undo.")]
    public static string System()
    {
        return LoadPrompt("system.md");
    }

    private static string LoadPrompt(string filename)
    {
        var filePath = Path.Combine(PromptsDirectory, filename);

        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }

        return $"# Prompt Not Found\n\nPrompt file '{filename}' not found.\n\nExpected location: {filePath}\n\nPlease ensure the Prompts directory is deployed with the application.";
    }
}
