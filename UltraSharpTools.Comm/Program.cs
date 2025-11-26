using UltraSharpTools.Comm;

public class Program
{
    public const string ApplicationName = "UltraSharpTools.Comm";
    public const string ApplicationVersion = "3.2.1";

    public static async Task Main(string[] args)
    {
        // Show help if requested
        if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
        {
            Console.WriteLine($"{ApplicationName} v{ApplicationVersion}");
            Console.WriteLine("Lightweight proxy for UltraSharpTools MCP server.");
            Console.WriteLine("Usage: Designed to be launched by MCP clients (Claude Desktop).");
            return;
        }

        if (args.Length > 0 && (args[0] == "--version" || args[0] == "-v"))
        {
            Console.WriteLine($"{ApplicationName} v{ApplicationVersion}");
            return;
        }

        // Start Droid and proxy MCP messages (forward all args to Droid)
        var processManager = new DroidProcessManager(args);
        var mcpBridge = new McpBridge(processManager);

        try
        {
#pragma warning disable CA2000 // Console streams live until app termination
            await mcpBridge.RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput());
#pragma warning restore CA2000
        }
        catch
        {
            // If proxy fails, just exit with error code
            // Claude Desktop will detect the failure
            Environment.Exit(1);
        }
    }
}
