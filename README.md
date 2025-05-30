# SharpTools: AI-Powered C# Code Analysis & Modification Engine

SharpTools is a robust service designed to empower AI agents with advanced capabilities for understanding, analyzing, and modifying C# codebases. It leverages the .NET Compiler Platform (Roslyn) to provide deep static analysis and precise code manipulation, going far beyond simple text-based operations.

SharpTools is designed to give AI the same insights and tools a human developer relies on, leading to more intelligent and reliable code assistance. It is effectively a simple IDE, made for an AI user.

Due to the comprehensive nature of the suite, it can almost be used completely standalone for editing existing C# solutions. If you use the SSE server and port forward your router, I think it's even possible to have Claude's web chat ui connect to this and have it act as a full coding assistant.

## Prompts

The included [Identity Prompt](Prompts/identity.prompt) is my personal C# coding assistant prompt, and it works well in combination with this suite. You're welcome to use it as is, modify it to match your preferences, or omit it entirely.

In VS Code, set it as your `copilot-instructions.md` to have it included in every interaction.

The [Tool Use Prompt](Prompts/github-copilot-sharptools.prompt) is fomulated specifically for Github Copilot's Agent mode. It overrides specific sections within the Copilot Agent System Prompt so that it avoids the built in tools.

It is available as an MCP prompt as well, so within Copilot, you just need to type `/mcp`, and it should show up as an option.

Something similar will be necessary for other coding assistants to prevent them from using their own default editing tools.

I recommend crafting custom tool use prompts for each agent you use this with, based on their [individual system prompts](https://github.com/x1xhlol/system-prompts-and-models-of-ai-tools).

## Note

This is a personal project, slapped together over a few weeks, and built in no small part by its own tools. It generally works well as it is, but the code is still fairly ugly, and some of the features are still quirky (like removing newlines before and after overwritten members).

I intend to maintain and improve it for as long as I am using it, and I welcome feedback and contributions as a part of that.

## Features

*   **Intelligent Code "Map" & "Mini-Map":**
    *   **Project-Wide Visibility:** SharpTools builds a "map" of your entire project, showing how namespaces, classes, and members are structured. It smartly adjusts the level of detail, allowing the AI to grasp the overall architecture or zoom into specific components without getting lost in the weeds.
    *   **Contextual Navigation Aids:** As the AI works, SharpTools provides simplified "mini-maps" (like call graphs and dependency trees) showing how the current piece of code connects to others. This helps the AI understand the local context and potential impacts of changes.

*   **Understanding Code, Inside and Out:**
    *   **Sees Inside Dependencies:** When the AI needs to understand code from an external library (like a NuGet package), SharpTools doesn't just see a black box. It actively tries to find the original source code (using industry standards like SourceLink or embedded PDBs) or, if necessary, decompiles the library to reveal its workings.
    *   **Handles Imperfect Naming:** AI (and humans!) don't always get names perfectly right. If the AI refers to a class or method with a slight variation (e.g., `Myclass` instead of `MyClass`, or omitting generic details), SharpTools intelligently figures out the correct symbol in your codebase.

*   **Safe and Controlled Code Changes:**
    *   **Every Change is Versioned:** All code modifications made through SharpTools are automatically committed to a special, timestamped Git branch. This creates a clear audit trail and makes it easy to review or revert any changes.
    *   **An "Undo" Button for AI:** If a change doesn't go as planned, the AI can easily "undo" the last set of modifications, rolling back to the previous state on its dedicated Git branch.
    *   **Precise, Surgical Edits:** Rather than rewriting entire files, the AI uses tools to make targeted changes – like adding a single method, renaming a variable, or replacing specific references. This minimizes unintended side effects.

*   **High-Signal, Concise AI Feedback:**
    *   SharpTools prioritizes clear and efficient communication with the AI. Instead of overwhelming it with raw data, it provides information-dense feedback. For example, after a code modification, the AI receives a precise diff highlighting exactly what was altered, rather than the entire rewritten file. This allows the AI to quickly confirm actions, understand the impact of its operations, and reduces unnecessary token usage.

*   **Proactive Code Quality Assistance:**
    *   **Immediate Compiler Feedback:** After any code change, SharpTools instantly checks for new compilation errors and reports them back. This is independent of any similar checks the AI platform might have, providing an extra layer of validation.
    *   **Complexity Alerts:** If the AI generates a method or class that seems overly complex, SharpTools will flag it, encouraging the AI to produce simpler, more maintainable code.
    *   **Duplicate Code Detection:** When the AI adds new code, SharpTools attempts to detect if it's very similar to existing code, helping to prevent redundancy and keep the codebase cleaner.

*   **Broad Compatibility & Consistency:**
    *   **Works with Your Projects:** Supports both modern (.NET SDK-style) and older (legacy .csproj) C# solutions, ensuring wide applicability.
    *   **Respects Your Coding Style:** SharpTools can identify and use your project's `.editorconfig` file to ensure that any code it modifies or adds aligns with your team's established formatting guidelines.

## Exposed Tools

SharpTools exposes a variety of "SharpTool_*" functions via MCP. Here's a brief overview categorized by their respective service files:

### Solution Tools

*   `SharpTool_LoadSolution`: Initializes the workspace with a given `.sln` file. This is the primary entry point.
*   `SharpTool_LoadProject`: Provides a detailed structural overview of a specific project within the loaded solution, including namespaces and types, to aid AI understanding of the project's layout.

### Analysis Tools

*   `SharpTool_GetMembers`: Lists members (methods, properties, etc.) of a type, including signatures and XML documentation.
*   `SharpTool_ViewDefinition`: Displays the source code of a symbol (class, method, etc.), including contextual information like call graphs or type references.
*   `SharpTool_ListImplementations`: Finds all implementations of an interface/abstract method or derived classes of a base class.
*   `SharpTool_FindReferences`: Locates all usages of a symbol across the solution, providing contextual code snippets.
*   `SharpTool_SearchDefinitions`: Performs a regex-based search across symbol declarations and signatures in both source code and compiled assemblies.
*   `SharpTool_ManageUsings`: Reads or overwrites using directives in a document.
*   `SharpTool_ManageAttributes`: Reads or overwrites attributes on a specific declaration.
*   `SharpTool_AnalyzeComplexity`: Performs complexity analysis (cyclomatic, cognitive, coupling, etc.) on methods, classes, or projects.
*   ~(Disabled) `SharpTool_GetAllSubtypes`: Recursively lists all nested members of a type.~
*   ~(Disabled) `SharpTool_ViewInheritanceChain`: Shows the inheritance hierarchy for a type.~
*   ~(Disabled) `SharpTool_ViewCallGraph`: Displays incoming and outgoing calls for a method.~
*   ~(Disabled) `SharpTool_FindPotentialDuplicates`: Finds semantically similar methods or classes.~

### Document Tools

*   `SharpTool_ReadRawFromRoslynDocument`: Reads the raw content of a file (indentation omitted).
*   `SharpTool_CreateRoslynDocument`: Creates a new file with specified content.
*   `SharpTool_OverwriteRoslynDocument`: Overwrites an existing file with new content.
*   `SharpTool_ReadTypesFromRoslynDocument`: Lists all types and their members defined within a specific source file.

### Modification Tools

*   `SharpTool_AddMember`: Adds a new member (method, property, field, nested type, etc.) to a specified type.
*   `SharpTool_OverwriteMember`: Replaces the definition of an existing member or type with new code, or deletes it.
*   `SharpTool_RenameSymbol`: Renames a symbol and updates all its references throughout the solution.
*   `SharpTool_FindAndReplace`: Performs regex-based find and replace operations within a specified symbol's declaration or across files matching a glob pattern.
*   `SharpTool_MoveMember`: Moves a member from one type/namespace to another.
*   `SharpTool_Undo`: Reverts the last applied change using Git integration.
*   ~(Disabled) `SharpTool_ReplaceAllReferences`: Replaces all references to a symbol with specified C# code.~

### Package Tools

*   ~(Disabled) `SharpTool_AddOrModifyNugetPackage`: Adds or updates a NuGet package reference in a project file.~

### Misc Tools

*   `SharpTool_RequestNewTool`: Allows the AI to request new tools or features, logging the request for human review.

## Prerequisites

*   .NET 8 SDK (or as specified by the project files).
*   To build and run, ensure you have the necessary MSBuild components. This is typically included with Visual Studio or .NET SDK's workload for "Desktop development with C++" (which includes MSBuild targets even for C# projects) or by installing the Visual Studio Build Tools.

## Building

To build the entire solution:
```bash
dotnet build SharpTools.sln
```
This will build all services and server applications.

## Running the Servers

### SSE Server (HTTP)

The SSE server hosts the tools on an HTTP endpoint.

```bash
# Navigate to the SseServer project directory
cd SharpTools.SseServer

# Run with default options (port 3001)
dotnet run

# Run with specific options
dotnet run -- --port 3005 --log-file ./logs/mcp-sse-server.log --log-level Debug --load-solution "/path/to/your/solution.sln"
```
Key Options:
*   `--port <number>`: Port to listen on (default: 3001).
*   `--log-file <path>`: Path to a log file.
*   `--log-level <level>`: Minimum log level (Verbose, Debug, Information, Warning, Error, Fatal).
*   `--load-solution <path>`: Path to a `.sln` file to load on startup.

### Stdio Server

The Stdio server communicates over standard input/output.

Configure it in your MCP client of choice.
VSCode Copilot example:

```json
"mcp": {
    "servers": {
        "SharpTools": {
            "type": "stdio",
            "command": "/path/to/repo/SharpToolsMCP/SharpTools.StdioServer/bin/Debug/net8.0/SharpTools.StdioServer",
            "args": [
                "--log-directory",
                "/var/log/sharptools/",
                "--log-level",
                "Debug",
            ]
        }
    }
},
```
Key Options:
*   `--log-directory <path>`: Directory to store log files.
*   `--log-level <level>`: Minimum log level.
*   `--load-solution <path>`: Path to a `.sln` file to load on startup.

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues.

## License

This project is licensed under the MIT License - see the LICENSE.md file for details.