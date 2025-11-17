# Pre-Commit Hook Integration Design

**Автоматическая проверка кода перед коммитом через сервер и LLM модель для КАЖДОГО разработчика.**

---

## 🎯 Концепция

```
Developer пишет код → git commit

       ↓ pre-commit hook (local)

ultrasharp-tool (network mode)
       ↓ отправляет changed files

RemoteServer + LLM Model (DeepSeek-Coder 16B)
       ↓ анализирует код

┌─────────────────────────────────────────────┐
│ AI Analysis (2-5 seconds):                  │
│ ✅ SQL Injection check                      │
│ ✅ XSS vulnerability scan                   │
│ ✅ Hardcoded secrets detection              │
│ ✅ API contract violations                  │
│ ✅ Code complexity (CC > 15)                │
│ ✅ JIRA task alignment                      │
│ ✅ Code quality recommendations             │
└─────────────────────────────────────────────┘

       ↓ результаты анализа

┌─ Все OK? ────────────────────┐
│  ✅ YES → Commit allowed     │
│  ❌ NO → Commit BLOCKED      │
│           + детальный отчёт  │
└──────────────────────────────┘
```

**Ключевые особенности:**
- ✅ Автоматическая установка hook при первом запуске ultrasharp-tool
- ✅ Анализ только изменённых файлов (fast!)
- ✅ Offline fallback (если сервер недоступен)
- ✅ Configurable checks (можно отключать)
- ✅ Детальные отчёты с рекомендациями

---

## 🏗️ Архитектура

### Компоненты

```
┌──────────────────────────────────────────────────────┐
│ Developer Machine                                    │
│                                                      │
│  ┌────────────────────────────────────────────┐     │
│  │ .git/hooks/pre-commit (shell script)       │     │
│  │                                            │     │
│  │ #!/bin/bash                                │     │
│  │ ultrasharp-tool pre-commit-check           │     │
│  └────────────────┬───────────────────────────┘     │
│                   │                                  │
│                   ↓                                  │
│  ┌────────────────────────────────────────────┐     │
│  │ ultrasharp-tool (network mode)             │     │
│  │                                            │     │
│  │ Commands:                                  │     │
│  │ • pre-commit-check                         │     │
│  │ • install-hooks                            │     │
│  │ • uninstall-hooks                          │     │
│  └────────────────┬───────────────────────────┘     │
│                   │                                  │
│                   ↓ HTTP POST /api/pre-commit      │
└───────────────────┼──────────────────────────────────┘
                    │
                    ↓
┌──────────────────────────────────────────────────────┐
│ RemoteServer (GPU + LLM)                             │
│                                                      │
│  ┌────────────────────────────────────────────┐     │
│  │ API: POST /api/pre-commit                  │     │
│  │                                            │     │
│  │ Input:                                     │     │
│  │ • changed files (code)                     │     │
│  │ • commit message                           │     │
│  │ • user config                              │     │
│  │                                            │     │
│  │ Processing:                                │     │
│  │ 1. VulnerabilityScanner                    │     │
│  │ 2. ContractChecker                         │     │
│  │ 3. ComplexityAnalyzer                      │     │
│  │ 4. JIRAAlignmentChecker                    │     │
│  │ 5. LLM Analysis (DeepSeek-Coder)           │     │
│  │                                            │     │
│  │ Output:                                    │     │
│  │ • issues[] (vulnerabilities, violations)   │     │
│  │ • recommendations[]                        │     │
│  │ • verdict: "allowed" | "blocked" | "warn"  │     │
│  └────────────────────────────────────────────┘     │
│                                                      │
│  ┌────────────────────────────────────────────┐     │
│  │ LLM: DeepSeek-Coder-V2-Lite 16B            │     │
│  │                                            │     │
│  │ Prompt template:                           │     │
│  │ "Analyze this C# code for:                 │     │
│  │  - Security vulnerabilities                │     │
│  │  - Performance issues                      │     │
│  │  - Code quality problems                   │     │
│  │  - Best practice violations"               │     │
│  └────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────┘
```

---

## 📦 Implementation

### 1. Git Hook Installation

**ultrasharp-tool CLI command:**

```bash
# Автоматическая установка hooks
ultrasharp-tool install-hooks --repo /path/to/repo

# С конфигурацией
ultrasharp-tool install-hooks \
  --repo /path/to/repo \
  --server https://ultrasharp-server.company.com \
  --checks "vulnerability,contracts,complexity,jira"
```

**Что происходит:**

```csharp
// Commands/InstallHooksCommand.cs
public class InstallHooksCommand
{
    public async Task Execute(string repoPath, string serverUrl, string[] checks)
    {
        var hooksDir = Path.Combine(repoPath, ".git", "hooks");
        var preCommitPath = Path.Combine(hooksDir, "pre-commit");

        // Create pre-commit hook script
        var hookScript = GenerateHookScript(serverUrl, checks);

        // Write script
        await File.WriteAllTextAsync(preCommitPath, hookScript);

        // Make executable (Linux/Mac)
        if (!OperatingSystem.IsWindows())
        {
            Process.Start("chmod", $"+x {preCommitPath}");
        }

        // Save config
        var config = new PreCommitConfig
        {
            ServerUrl = serverUrl,
            Checks = checks,
            Timeout = TimeSpan.FromSeconds(30),
            OfflineFallback = true
        };

        await SaveConfig(repoPath, config);

        Console.WriteLine($"✅ Pre-commit hooks installed in {repoPath}");
        Console.WriteLine($"   Server: {serverUrl}");
        Console.WriteLine($"   Checks: {string.Join(", ", checks)}");
    }

    private string GenerateHookScript(string serverUrl, string[] checks)
    {
        return $@"#!/bin/bash
# ultrasharp-tools pre-commit hook
# Auto-generated - do not edit manually

# Get path to ultrasharp-tool
ULTRASHARP_TOOL=""$(which ultrasharp-tool)""

if [ -z ""$ULTRASHARP_TOOL"" ]; then
    echo ""❌ ultrasharp-tool not found in PATH""
    exit 1
fi

# Run pre-commit check
""$ULTRASHARP_TOOL"" pre-commit-check \
    --server ""{serverUrl}"" \
    --checks ""{string.Join(",", checks)}"" \
    --repo ""$(pwd)""

# Exit with the same code
exit $?
";
    }
}
```

---

### 2. Pre-Commit Check Implementation

**ultrasharp-tool command:**

```csharp
// Commands/PreCommitCheckCommand.cs
public class PreCommitCheckCommand
{
    private readonly IRemoteServerClient _serverClient;
    private readonly IGitService _gitService;

    public async Task<int> Execute(string repoPath, string serverUrl, string[] checks)
    {
        Console.WriteLine("🔍 Running pre-commit checks...");

        try
        {
            // 1. Get changed files
            var changedFiles = await _gitService.GetStagedFiles(repoPath);

            if (!changedFiles.Any())
            {
                Console.WriteLine("   No files to check");
                return 0; // Success (no files)
            }

            Console.WriteLine($"   Checking {changedFiles.Count} files...");

            // 2. Read file contents
            var fileContents = new Dictionary<string, string>();
            foreach (var file in changedFiles)
            {
                var content = await File.ReadAllTextAsync(Path.Combine(repoPath, file));
                fileContents[file] = content;
            }

            // 3. Get commit message (if exists)
            var commitMessage = await _gitService.GetPreparedCommitMessage(repoPath);

            // 4. Send to server for analysis
            var request = new PreCommitCheckRequest
            {
                Files = fileContents,
                CommitMessage = commitMessage,
                Checks = checks,
                Project = Path.GetFileName(repoPath)
            };

            var result = await _serverClient.PostAsync<PreCommitCheckResult>(
                $"{serverUrl}/api/pre-commit/check",
                request
            );

            // 5. Display results
            return DisplayResults(result);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == null)
        {
            // Server unreachable
            Console.WriteLine("⚠️  Server unreachable, using offline fallback");
            return await OfflineFallbackCheck(changedFiles);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return 1; // Block commit
        }
    }

    private int DisplayResults(PreCommitCheckResult result)
    {
        Console.WriteLine();

        if (result.Verdict == "allowed")
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ All checks passed!");
            Console.ResetColor();
            return 0; // Success
        }

        if (result.Verdict == "warn")
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  Warnings found (commit allowed):");
            Console.ResetColor();

            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"   • {warning.Type}: {warning.Message}");
                Console.WriteLine($"     File: {warning.File}:{warning.Line}");
                Console.WriteLine($"     Recommendation: {warning.Recommendation}");
                Console.WriteLine();
            }

            return 0; // Success (with warnings)
        }

        // Blocked
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ Commit BLOCKED - critical issues found:");
        Console.ResetColor();
        Console.WriteLine();

        foreach (var issue in result.Issues)
        {
            Console.WriteLine($"   🔴 {issue.Severity}: {issue.Type}");
            Console.WriteLine($"      File: {issue.File}:{issue.Line}");
            Console.WriteLine($"      {issue.Description}");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"      💡 Fix: {issue.Recommendation}");
            Console.ResetColor();
            Console.WriteLine();
        }

        Console.WriteLine("Fix these issues and try again.");
        Console.WriteLine();

        return 1; // Failure (commit blocked)
    }
}
```

---

### 3. Server-Side Analysis

**RemoteServer API endpoint:**

```csharp
// Controllers/PreCommitController.cs
[ApiController]
[Route("api/pre-commit")]
public class PreCommitController : ControllerBase
{
    private readonly IVulnerabilityScanner _vulnerabilityScanner;
    private readonly IContractChecker _contractChecker;
    private readonly IComplexityAnalyzer _complexityAnalyzer;
    private readonly IJiraAlignmentChecker _jiraChecker;
    private readonly ILlmAnalysisService _llmAnalysis;

    [HttpPost("check")]
    public async Task<PreCommitCheckResult> Check([FromBody] PreCommitCheckRequest request)
    {
        var issues = new List<Issue>();
        var warnings = new List<Warning>();

        // Run checks in parallel
        var tasks = new List<Task>();

        if (request.Checks.Contains("vulnerability"))
        {
            tasks.Add(Task.Run(async () =>
            {
                foreach (var (file, content) in request.Files)
                {
                    var vulns = await _vulnerabilityScanner.ScanCode(content);
                    issues.AddRange(vulns.Select(v => new Issue
                    {
                        Type = v.Type,
                        Severity = v.Severity,
                        File = file,
                        Line = v.Line,
                        Description = v.Description,
                        Recommendation = v.Recommendation
                    }));
                }
            }));
        }

        if (request.Checks.Contains("contracts"))
        {
            tasks.Add(Task.Run(async () =>
            {
                foreach (var (file, content) in request.Files)
                {
                    var violations = await _contractChecker.CheckContracts(file, content);
                    issues.AddRange(violations.Select(v => new Issue
                    {
                        Type = "Contract Violation",
                        Severity = v.Severity,
                        File = file,
                        Description = v.Description,
                        Recommendation = v.Recommendation
                    }));
                }
            }));
        }

        if (request.Checks.Contains("complexity"))
        {
            tasks.Add(Task.Run(async () =>
            {
                foreach (var (file, content) in request.Files)
                {
                    var complexities = await _complexityAnalyzer.Analyze(content);
                    var highComplexity = complexities.Where(c => c.Cyclomatic > 15);

                    warnings.AddRange(highComplexity.Select(c => new Warning
                    {
                        Type = "High Complexity",
                        File = file,
                        Line = c.Line,
                        Message = $"Cyclomatic complexity: {c.Cyclomatic}",
                        Recommendation = "Consider refactoring to reduce complexity"
                    }));
                }
            }));
        }

        if (request.Checks.Contains("jira") && !string.IsNullOrEmpty(request.CommitMessage))
        {
            tasks.Add(Task.Run(async () =>
            {
                var alignment = await _jiraChecker.CheckAlignment(
                    request.CommitMessage,
                    request.Files
                );

                if (alignment.Similarity < 0.7)
                {
                    warnings.Add(new Warning
                    {
                        Type = "JIRA Alignment",
                        Message = $"Code changes don't match JIRA task {alignment.TicketId}",
                        Recommendation = "Verify that changes address the task requirements"
                    });
                }
            }));
        }

        // Wait for all checks
        await Task.WhenAll(tasks);

        // LLM analysis (last, most expensive)
        if (request.Checks.Contains("llm"))
        {
            var llmResults = await AnalyzeWithLLM(request.Files);
            issues.AddRange(llmResults.Issues);
            warnings.AddRange(llmResults.Warnings);
        }

        // Determine verdict
        var hasBlockingIssues = issues.Any(i =>
            i.Severity == "Critical" || i.Severity == "High");

        return new PreCommitCheckResult
        {
            Verdict = hasBlockingIssues ? "blocked" :
                      warnings.Any() ? "warn" :
                      "allowed",
            Issues = issues,
            Warnings = warnings,
            CheckedFiles = request.Files.Count,
            Duration = TimeSpan.FromSeconds(2.5)
        };
    }

    private async Task<LlmAnalysisResult> AnalyzeWithLLM(Dictionary<string, string> files)
    {
        var issues = new List<Issue>();
        var warnings = new List<Warning>();

        foreach (var (file, content) in files)
        {
            // Skip non-code files
            if (!file.EndsWith(".cs") && !file.EndsWith(".js") && !file.EndsWith(".py"))
                continue;

            var prompt = $@"Analyze this code for:
- Security vulnerabilities (SQL injection, XSS, etc.)
- Performance issues (N+1 queries, inefficient algorithms)
- Code quality problems (high complexity, duplicates)
- Best practice violations

Code:
```
{content}
```

Return JSON:
{{
  ""issues"": [
    {{
      ""type"": ""SQL Injection"",
      ""severity"": ""Critical"",
      ""line"": 45,
      ""description"": ""...\",
      ""recommendation"": ""...""
    }}
  ],
  ""warnings"": [...]
}}";

            var response = await _llmAnalysis.Analyze(prompt);
            var result = JsonSerializer.Deserialize<LlmAnalysisResponse>(response);

            foreach (var issue in result.Issues)
            {
                issue.File = file;
                issues.Add(issue);
            }

            foreach (var warning in result.Warnings)
            {
                warning.File = file;
                warnings.Add(warning);
            }
        }

        return new LlmAnalysisResult
        {
            Issues = issues,
            Warnings = warnings
        };
    }
}
```

---

### 4. LLM Analysis Service

```csharp
// Services/LlmAnalysisService.cs
public class LlmAnalysisService : ILlmAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly string _llmUrl;

    public LlmAnalysisService(IConfiguration config)
    {
        _llmUrl = config["llm:url"];  // http://vllm-deepseek:8000
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async Task<string> Analyze(string prompt)
    {
        var request = new
        {
            model = "deepseek-ai/DeepSeek-Coder-V2-Lite-Instruct",
            messages = new[]
            {
                new { role = "system", content = "You are a code security and quality expert. Analyze code and return findings in JSON format." },
                new { role = "user", content = prompt }
            },
            temperature = 0.1,  // Low temperature для deterministic results
            max_tokens = 1000,
            response_format = new { type = "json_object" }  // Force JSON output
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_llmUrl}/v1/chat/completions",
            request
        );

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LlmResponse>();
        return result.Choices[0].Message.Content;
    }
}
```

---

## 🚀 User Experience

### Setup (один раз)

```bash
# 1. Установить ultrasharp-tool
wget https://github.com/yourorg/ultrasharp-tools/releases/download/v1.0/ultrasharp-tool-linux-x64
chmod +x ultrasharp-tool-linux-x64
sudo mv ultrasharp-tool-linux-x64 /usr/local/bin/ultrasharp-tool

# 2. Установить hooks в проекте
cd ~/projects/myapp
ultrasharp-tool install-hooks \
  --server https://ultrasharp-server.company.com \
  --checks "vulnerability,contracts,complexity,llm"

# Output:
# ✅ Pre-commit hooks installed in /home/user/projects/myapp
#    Server: https://ultrasharp-server.company.com
#    Checks: vulnerability, contracts, complexity, llm
#
# Hook installed at: .git/hooks/pre-commit
# Config saved at: .git/ultrasharp-config.json
```

---

### Daily Workflow

**Developer делает изменения:**

```bash
# Edit code
vim src/Services/UserService.cs

# Stage changes
git add src/Services/UserService.cs

# Try to commit
git commit -m "PROJ-123 Add email validation"
```

**Pre-commit hook запускается автоматически:**

```
🔍 Running pre-commit checks...
   Checking 1 files...
   [1/4] Vulnerability scan... ✅ (0.2s)
   [2/4] Contract check... ✅ (0.1s)
   [3/4] Complexity analysis... ⚠️  (0.3s)
   [4/4] LLM analysis... ✅ (2.1s)

⚠️  Warnings found (commit allowed):
   • High Complexity: ValidateEmail method
     File: src/Services/UserService.cs:45
     Cyclomatic complexity: 12 (target: < 10)
     Recommendation: Extract validation logic to separate methods

✅ Commit allowed (with warnings)
Total time: 2.7s
```

**Commit успешен!**

---

### Blocked Commit Example

```bash
git commit -m "Add login feature"
```

```
🔍 Running pre-commit checks...
   Checking 2 files...
   [1/4] Vulnerability scan... ❌ (0.3s)
   [2/4] Contract check... ✅ (0.1s)
   [3/4] Complexity analysis... ✅ (0.2s)
   [4/4] LLM analysis... ❌ (2.5s)

❌ Commit BLOCKED - critical issues found:

   🔴 Critical: SQL Injection
      File: src/Controllers/LoginController.cs:23
      Direct string concatenation in SQL query allows injection attacks

      💡 Fix: Use parameterized queries:
      var sql = "SELECT * FROM Users WHERE Username = @username";
      command.Parameters.AddWithValue("@username", username);

   🔴 High: Hardcoded Secret
      File: src/Services/AuthService.cs:12
      API key hardcoded in source code (security risk)

      💡 Fix: Move to configuration:
      var apiKey = configuration["ApiKey"];

Fix these issues and try again.

Total time: 3.1s
```

**Commit заблокирован!** Developer должен исправить проблемы.

---

## ⚙️ Configuration

**Project-level config (.git/ultrasharp-config.json):**

```json
{
  "preCommit": {
    "enabled": true,
    "serverUrl": "https://ultrasharp-server.company.com",
    "checks": [
      {
        "name": "vulnerability",
        "enabled": true,
        "blocking": true  // Block commit if found
      },
      {
        "name": "contracts",
        "enabled": true,
        "blocking": true
      },
      {
        "name": "complexity",
        "enabled": true,
        "blocking": false,  // Only warn
        "threshold": 15  // Block if CC > 15
      },
      {
        "name": "jira",
        "enabled": true,
        "blocking": false,
        "similarityThreshold": 0.7
      },
      {
        "name": "llm",
        "enabled": true,
        "blocking": true,
        "timeout": "60s"
      }
    ],
    "offlineFallback": true,
    "timeout": "30s",
    "excludeFiles": [
      "*.Designer.cs",
      "*.generated.cs",
      "**/bin/**",
      "**/obj/**"
    ]
  }
}
```

**User can override:**

```bash
# Disable specific check for one commit
git commit --no-verify  # Skip all hooks

# Or configure per-user
ultrasharp-tool config set preCommit.checks.llm.enabled false
```

---

## 📊 Performance

### Latency Breakdown

```
1. Get staged files (local):        ~50ms
2. Read file contents (local):      ~100ms
3. Send to server (network):        ~50-200ms
4. Server analysis:
   - Vulnerability scan:             ~200ms
   - Contract check:                 ~100ms
   - Complexity analysis:            ~300ms
   - LLM analysis (DeepSeek 16B):    ~2000ms
5. Receive results (network):       ~50ms

Total: ~2.5-3.5 seconds (acceptable!)
```

**Optimization strategies:**
- ✅ Parallel checks (все кроме LLM одновременно)
- ✅ Cache results (same file hash = skip check)
- ✅ Batch multiple files в один LLM request
- ✅ Skip non-code files (.md, .json, etc.)

---

## 🎯 Roadmap Integration

### Phase 1: Basic Hooks (1 неделя)
- [ ] install-hooks command
- [ ] pre-commit-check command
- [ ] Git integration (staged files)
- [ ] Server API endpoint
- [ ] Basic vulnerability scan

### Phase 2: Advanced Checks (1-2 недели)
- [ ] Contract checker
- [ ] Complexity analyzer
- [ ] JIRA alignment
- [ ] Offline fallback

### Phase 3: LLM Integration (1 неделя)
- [ ] LLM analysis service
- [ ] DeepSeek-Coder integration
- [ ] JSON response parsing
- [ ] Error handling

### Phase 4: User Experience (1 неделя)
- [ ] Colored output
- [ ] Progress indicators
- [ ] Detailed error messages
- [ ] Auto-fix suggestions

**Total: ~4-5 недель**

---

## 🎉 Benefits

**Для разработчиков:**
- ✅ Ловит ошибки **ДО** code review
- ✅ Instant feedback (2-3 seconds)
- ✅ Learn best practices (recommendations)
- ✅ Меньше rejected PRs

**Для команды:**
- ✅ Consistent code quality
- ✅ Security vulnerabilities пойманы рано
- ✅ Меньше времени на code review
- ✅ Technical debt prevention

**Для компании:**
- ✅ Reduced security incidents
- ✅ Faster development velocity
- ✅ Better codebase health
- ✅ Compliance (audit trail)

---

**Готов добавить в roadmap и начать реализацию?**
