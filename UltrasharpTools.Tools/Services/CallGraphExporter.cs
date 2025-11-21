using UltrasharpTools.Tools.Models;
using UltrasharpTools.Tools.Infrastructure;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Exports call graph visualizations in various formats (DOT, Mermaid, GraphML).
/// </summary>
public static class CallGraphExporter
{
    /// <summary>
    /// Exports backtrace result to Graphviz DOT format.
    /// </summary>
    public static string ToDot(BacktraceResult result, bool includeConfidence = true)
    {
        var sb = ObjectPoolProvider.Instance.GetStringBuilder();
        sb.AppendLine("digraph CallGraph {");
        sb.AppendLine("  rankdir=BT;  // Bottom to top (crash -> entry)");
        sb.AppendLine("  node [shape=box, style=rounded];");
        sb.AppendLine();

        // Color scheme based on confidence
        sb.AppendLine("  // Color scheme:");
        sb.AppendLine("  // High confidence (>0.8): green");
        sb.AppendLine("  // Medium confidence (0.5-0.8): yellow");
        sb.AppendLine("  // Low confidence (<0.5): red");
        sb.AppendLine();

        var nodeId = 0;
        var nodeMapping = new Dictionary<string, string>();

        foreach (var path in result.CallPaths)
        {
            sb.AppendLine($"  // Path #{path.PathId} (Confidence: {path.Confidence:P0})");

            for (int i = 0; i < path.Frames.Count; i++)
            {
                var frame = path.Frames[i];
                var currentNodeId = $"node_{nodeId++}";

                // Create node
                var methodName = SimplifyMethodName(frame.MethodFqn);
                var label = $"{methodName}";

                if (includeConfidence && frame.MatchesStackTrace)
                {
                    label += $"\\n[{frame.StackTraceConfidence:P0}]";
                }

                // Color based on confidence
                var color = GetConfidenceColor(frame.StackTraceConfidence);
                var style = frame.MatchesStackTrace ? "filled" : "rounded";

                sb.AppendLine($"  {currentNodeId} [label=\"{EscapeDot(label)}\", fillcolor=\"{color}\", style=\"{style}\"];");

                // Store mapping
                nodeMapping[frame.MethodFqn] = currentNodeId;

                // Create edge to next frame (caller)
                if (i < path.Frames.Count - 1)
                {
                    var nextFrame = path.Frames[i + 1];
                    var nextNodeId = $"node_{nodeId}";

                    sb.AppendLine($"  {currentNodeId} -> {nextNodeId};");
                }
            }

            sb.AppendLine();
        }

        // Add legend
        sb.AppendLine("  // Legend");
        sb.AppendLine("  subgraph cluster_legend {");
        sb.AppendLine("    label=\"Confidence Legend\";");
        sb.AppendLine("    legend_high [label=\"High (>0.8)\", fillcolor=\"lightgreen\", style=\"filled\"];");
        sb.AppendLine("    legend_medium [label=\"Medium (0.5-0.8)\", fillcolor=\"yellow\", style=\"filled\"];");
        sb.AppendLine("    legend_low [label=\"Low (<0.5)\", fillcolor=\"lightcoral\", style=\"filled\"];");
        sb.AppendLine("  }");

        sb.AppendLine("}");

        var result_string = sb.ToString();
        ObjectPoolProvider.Instance.ReturnStringBuilder(sb);
        return result_string;
    }

    /// <summary>
    /// Exports backtrace result to Mermaid flowchart format.
    /// </summary>
    public static string ToMermaid(BacktraceResult result, bool includeConfidence = true)
    {
        var sb = ObjectPoolProvider.Instance.GetStringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph BT");
        sb.AppendLine("  %% Call graph from crash point to entry");
        sb.AppendLine();

        var nodeId = 0;
        var nodeMapping = new Dictionary<string, string>();

        foreach (var path in result.CallPaths)
        {
            sb.AppendLine($"  %% Path #{path.PathId} (Confidence: {path.Confidence:P0})");

            for (int i = 0; i < path.Frames.Count; i++)
            {
                var frame = path.Frames[i];
                var currentNodeId = $"N{nodeId++}";

                // Create node
                var methodName = SimplifyMethodName(frame.MethodFqn);
                var label = methodName;

                if (includeConfidence && frame.MatchesStackTrace)
                {
                    label += $" [{frame.StackTraceConfidence:P0}]";
                }

                // Style based on confidence
                var style = GetMermaidStyle(frame.StackTraceConfidence, frame.MatchesStackTrace);

                sb.AppendLine($"  {currentNodeId}[\"{EscapeMermaid(label)}\"]");
                if (!string.IsNullOrEmpty(style))
                {
                    sb.AppendLine($"  style {currentNodeId} {style}");
                }

                // Store mapping
                nodeMapping[frame.MethodFqn] = currentNodeId;

                // Create edge to next frame (caller)
                if (i < path.Frames.Count - 1)
                {
                    var nextFrame = path.Frames[i + 1];
                    var nextNodeId = $"N{nodeId}";

                    sb.AppendLine($"  {currentNodeId} --> {nextNodeId}");
                }
            }

            sb.AppendLine();
        }

        sb.AppendLine("```");

        var result_string = sb.ToString();
        ObjectPoolProvider.Instance.ReturnStringBuilder(sb);
        return result_string;
    }

    /// <summary>
    /// Exports backtrace result to GraphML format (compatible with yEd, Gephi, etc).
    /// </summary>
    public static string ToGraphML(BacktraceResult result)
    {
        var sb = ObjectPoolProvider.Instance.GetStringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<graphml xmlns=\"http://graphml.graphdrawing.org/xmlns\"");
        sb.AppendLine("         xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"");
        sb.AppendLine("         xsi:schemaLocation=\"http://graphml.graphdrawing.org/xmlns");
        sb.AppendLine("         http://graphml.graphdrawing.org/xmlns/1.0/graphml.xsd\">");
        sb.AppendLine();

        // Define keys for attributes
        sb.AppendLine("  <key id=\"d0\" for=\"node\" attr.name=\"label\" attr.type=\"string\"/>");
        sb.AppendLine("  <key id=\"d1\" for=\"node\" attr.name=\"confidence\" attr.type=\"double\"/>");
        sb.AppendLine("  <key id=\"d2\" for=\"node\" attr.name=\"matchesStackTrace\" attr.type=\"boolean\"/>");
        sb.AppendLine("  <key id=\"d3\" for=\"edge\" attr.name=\"pathId\" attr.type=\"int\"/>");
        sb.AppendLine();

        sb.AppendLine("  <graph id=\"CallGraph\" edgedefault=\"directed\">");

        var nodeId = 0;
        var nodeMapping = new Dictionary<string, string>();

        foreach (var path in result.CallPaths)
        {
            sb.AppendLine($"    <!-- Path #{path.PathId} -->");

            for (int i = 0; i < path.Frames.Count; i++)
            {
                var frame = path.Frames[i];
                var currentNodeId = $"n{nodeId++}";

                // Create node
                sb.AppendLine($"    <node id=\"{currentNodeId}\">");
                sb.AppendLine($"      <data key=\"d0\">{EscapeXml(SimplifyMethodName(frame.MethodFqn))}</data>");
                sb.AppendLine($"      <data key=\"d1\">{frame.StackTraceConfidence}</data>");
                sb.AppendLine($"      <data key=\"d2\">{frame.MatchesStackTrace.ToString().ToLower()}</data>");
                sb.AppendLine("    </node>");

                // Store mapping
                nodeMapping[frame.MethodFqn] = currentNodeId;

                // Create edge
                if (i < path.Frames.Count - 1)
                {
                    var nextFrame = path.Frames[i + 1];
                    var nextNodeId = $"n{nodeId}";
                    var edgeId = $"e{nodeId}";

                    sb.AppendLine($"    <edge id=\"{edgeId}\" source=\"{currentNodeId}\" target=\"{nextNodeId}\">");
                    sb.AppendLine($"      <data key=\"d3\">{path.PathId}</data>");
                    sb.AppendLine("    </edge>");
                }
            }
        }

        sb.AppendLine("  </graph>");
        sb.AppendLine("</graphml>");

        var result_string = sb.ToString();
        ObjectPoolProvider.Instance.ReturnStringBuilder(sb);
        return result_string;
    }

    private static string SimplifyMethodName(string fqn)
    {
        // Extract method name and type from FQN
        // Example: "MyNamespace.MyClass.MyMethod(int, string)" -> "MyClass.MyMethod"

        var parts = fqn.Split('.');
        if (parts.Length < 2)
        {
            return fqn;
        }

        // Get last two parts (Type.Method)
        var typeName = parts[^2];
        var methodPart = parts[^1];

        // Remove parameters
        var paramStart = methodPart.IndexOf('(');
        if (paramStart > 0)
        {
            methodPart = methodPart.Substring(0, paramStart);
        }

        return $"{typeName}.{methodPart}";
    }

    private static string GetConfidenceColor(double confidence)
    {
        if (confidence > 0.8) return "lightgreen";
        if (confidence > 0.5) return "yellow";
        return "lightcoral";
    }

    private static string GetMermaidStyle(double confidence, bool matches)
    {
        if (!matches) return "";

        if (confidence > 0.8) return "fill:#90EE90,stroke:#333,stroke-width:2px";
        if (confidence > 0.5) return "fill:#FFFF00,stroke:#333,stroke-width:2px";
        return "fill:#F08080,stroke:#333,stroke-width:2px";
    }

    private static string EscapeDot(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    private static string EscapeMermaid(string text)
    {
        return text.Replace("\"", "&quot;").Replace("[", "&#91;").Replace("]", "&#93;");
    }

    private static string EscapeXml(string text)
    {
        return text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;");
    }
}
