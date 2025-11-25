namespace UltrasharpTools.Tools.Services;

public partial class EditorConfigProvider : IEditorConfigProvider
{
    private readonly ILogger<EditorConfigProvider> _logger;
    private string? _solutionDirectory;
    private string? _rootEditorConfigPath;

    public EditorConfigProvider(ILogger<EditorConfigProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InitializeAsync(string solutionDirectory, CancellationToken cancellationToken)
    {
        _solutionDirectory =
            solutionDirectory ?? throw new ArgumentNullException(nameof(solutionDirectory));
        _rootEditorConfigPath = FindRootEditorConfig(_solutionDirectory);

        if (_rootEditorConfigPath != null)
        {
            LogEditorConfigFound(_rootEditorConfigPath);
        }
        else
        {
            LogEditorConfigNotFound();
        }
        return Task.CompletedTask;
    }

    public string? GetRootEditorConfigPath()
    {
        return _rootEditorConfigPath;
    }

    private string? FindRootEditorConfig(string startDirectory)
    {
        var currentDirectory = new DirectoryInfo(startDirectory);
        DirectoryInfo? repositoryRoot = null;

        // Traverse up to find .git directory (repository root)
        var tempDir = currentDirectory;
        while (tempDir != null)
        {
            if (Directory.Exists(Path.Combine(tempDir.FullName, ".git")))
            {
                repositoryRoot = tempDir;
                break;
            }
            tempDir = tempDir.Parent;
        }

        var limitDirectory = repositoryRoot ?? currentDirectory.Root;

        tempDir = currentDirectory;
        while (tempDir != null && tempDir.FullName.Length >= limitDirectory.FullName.Length)
        {
            var editorConfigPath = Path.Combine(tempDir.FullName, ".editorconfig");
            if (File.Exists(editorConfigPath))
            {
                return editorConfigPath;
            }
            if (tempDir.FullName == limitDirectory.FullName)
            {
                break; // Stop at repository root or drive root
            }
            tempDir = tempDir.Parent;
        }
        return null;
    }
}
