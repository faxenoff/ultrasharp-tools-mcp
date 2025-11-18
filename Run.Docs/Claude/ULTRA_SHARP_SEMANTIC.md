# Semantic Search & AI-Powered Features

[← Back to Overview](./ULTRA_SHARP.md)

**AI-powered code understanding** — find semantically similar code, compare semantic changes, leverage vector embeddings for intelligent code search.

---

## 📋 Quick Reference

| Tool | Purpose | Requires Setup | Use Case |
|------|---------|----------------|----------|
| **semantic_search** | Find semantically similar code | ✅ Yes | Find similar patterns, discover existing utilities |
| **semantic_diff** | Compare semantic changes | ✅ Yes | Verify refactorings, detect breaking changes |
| **detect_code_clones** | Find duplicate code | ✅ Yes | Identify consolidation opportunities |

---

## ⚙️ Setup Required

**Semantic tools require one-time setup:**

### Windows
```bash
setup-semantic-embedding.cmd
```

### Linux/macOS
```bash
pwsh Dev.Scripts/setup-semantic-embedding.ps1
```

### Provider Options

**1. TEI (Recommended)** — HuggingFace Text Embeddings Inference via Docker
- Best performance
- Runs locally in Docker
- No external API calls

**2. Ollama** — Local inference
- Models: granite-embedding, mxbai-embed-large
- Good for offline work
- Moderate performance

**3. Memory** — In-process (Experimental)
- For small codebases only
- No external dependencies
- Limited scalability

### Configuration

Config saved to: `Run.Config/semantic-config.json`

**Validation:**
```bash
pwsh Dev.Scripts/validate-semantic-config.ps1
```

---

## semantic_search

**Find semantically similar code** — searches for code with similar meaning/functionality using vector embeddings.

### Usage

```javascript
semantic_search(
    query: "validate email address",
    scope: "solution",
    topK: 10,
    minSimilarity: 0.7
)
```

### Parameters

- **query** (required): Natural language description or code snippet to search for
- **scope** (default: "solution"): Search scope (`"solution"`, `"project"`, `"namespace"`)
- **topK** (default: 10): Number of results to return (1-50)

### What It Shows

- 🔍 **Similarity score** (0-100%)
- 📄 **Code snippet** with context
- 📁 **Location** (file:line)
- 🆔 **FQN** for further analysis
- 📊 **Semantic relevance** explanation

### When to Use

✅ **For finding similar patterns:**
- Find duplicate logic
- Locate similar implementations
- Discover existing utilities

✅ **For refactoring:**
- Find all validation logic
- Locate similar error handling
- Identify consolidation opportunities

✅ **For code reuse:**
- Find existing implementations before writing new
- Discover utility methods
- Learn from existing patterns

✅ **For architectural analysis:**
- Find similar components
- Understand code organization
- Identify common patterns

### Example Queries

```javascript
// Natural language queries
"validate email address"
"parse JSON from HTTP response"
"handle database connection errors"
"calculate discount based on quantity"

// Code snippet queries
"if (user == null) throw new ArgumentNullException()"
"await _repository.GetByIdAsync(id)"
"try { ... } catch (Exception ex) { _logger.LogError(...) }"
```

### Best Practices

1. **Use natural language for concepts:**
   ```javascript
   // ✅ Good - conceptual search
   SemanticSearch(query: "user authentication logic")

   // ⚠️ Less effective - too specific
   SemanticSearch(query: "public bool AuthenticateUser")
   ```

2. **Use code snippets for patterns:**
   ```javascript
   // ✅ Good - pattern search
   SemanticSearch(query: "if (string.IsNullOrWhiteSpace(...))")

   // Find all similar null/whitespace checks
   ```

3. **Adjust topK based on needs:**
   ```javascript
   // Quick check - just top few
   SemanticSearch(query: "...", topK: 5)

   // Thorough search - more results
   SemanticSearch(query: "...", topK: 20)
   ```

4. **Narrow scope for faster searches:**
   ```javascript
   // Faster - specific project
   SemanticSearch(query: "...", scope: "MyProject.Core")

   // Slower - entire solution
   SemanticSearch(query: "...", scope: "solution")
   ```

### Performance

- **First search:** 5-15 sec (builds index)
- **Subsequent searches:** 1-3 sec (cached index)
- **Depends on:**
  - Codebase size
  - Embedding provider (TEI faster than Ollama)
  - Number of results (topK)

### Related Tools

- ➡️ [**ViewDefinition**](./ULTRA_SHARP_ANALYSIS.md#view_definition) — view found code details
- ➡️ [**FindReferences**](./ULTRA_SHARP_ANALYSIS.md#find_references) — find usages of similar code
- ➡️ [**AnalyzeComplexity**](./ULTRA_SHARP_ANALYSIS.md#analyze_complexity) — analyze complexity of found code

---

## semantic_diff

**Semantic change analysis** — compares code changes semantically, not just textually.

### Usage

```javascript
semantic_diff(
    beforeFqn: "MyNamespace.UserService.ValidateUser",
    afterFqn: "MyNamespace.UserService.ValidateUser",  // after modification
    includeImplementationDetails: false
)
```

### Parameters

- **beforeFqn** (required): FQN of method/class before changes
- **afterFqn** (required): FQN of method/class after changes (can be same if modified)
- **includeImplementationDetails** (default: false): Include low-level implementation changes

### What It Shows

- 🔄 **Semantic changes** (behavior modifications)
- 📊 **Similarity score** (how much semantic meaning changed)
- 🎯 **Intent preservation** (whether purpose remained same)
- 📝 **Change description** (what changed semantically)
- ⚠️ **Breaking changes** (potential issues)

### When to Use

✅ **For code review:**
- Understand semantic impact of changes
- Verify refactoring didn't change behavior
- Identify unintended changes

✅ **For refactoring verification:**
- Ensure behavior preserved
- Check intent maintained
- Validate optimization correctness

✅ **For documentation:**
- Explain what changed at semantic level
- Generate meaningful commit messages
- Create change summaries

### Best Practices

1. **Use before refactoring:**
   ```javascript
   // Before
   view_definition("UserService.ValidateUser")

   // Refactor...
   modify_code(...)

   // After - verify semantic equivalence
   SemanticDiff(
       beforeFqn: "UserService.ValidateUser",
       afterFqn: "UserService.ValidateUser"
   )
   ```

2. **Check breaking changes:**
   ```javascript
   SemanticDiff(...)
   // Output: "⚠️ Breaking change: validation logic now more strict"
   ```

3. **Exclude implementation details for high-level view:**
   ```javascript
   // High-level semantic changes only
   SemanticDiff(..., includeImplementationDetails: false)

   // All changes including implementation
   SemanticDiff(..., includeImplementationDetails: true)
   ```

### Performance

- **Speed:** 2-5 sec per comparison
- **Depends on:**
  - Code size
  - Embedding provider
  - Implementation detail level

### Related Tools

- ⬅️ [**view_definition**](./ULTRA_SHARP_ANALYSIS.md#view_definition) — see before/after code
- ⬅️ [**modify_code**](./ULTRA_SHARP_MODIFICATION.md#modify_code) — make changes to compare
- ➡️ [**analyze_complexity**](./ULTRA_SHARP_ANALYSIS.md#analyze_complexity) — compare complexity before/after

---

## detect_code_clones

**Find duplicate or similar code** — identifies code clones across the codebase using semantic analysis.

### Usage

```javascript
detect_code_clones(
    minSimilarity: 0.85,
    mode: "semantic",
    membersOnly: true,
    maxGroups: 20
)
```

### Parameters

- **minSimilarity** (default: 0.85): Minimum similarity threshold (0.5-1.0). Use 0.85+ for exact duplicates, 0.7+ for similar patterns.
- **mode** (default: "semantic"): Clone detection mode - 'semantic' (ML-based), 'exact', or 'similar'
- **membersOnly** (default: true): Only analyze methods and properties, skip classes for faster analysis
- **maxGroups** (default: 20): Maximum number of clone groups to return (1-100)

### What It Shows

- 🔍 **Clone groups** - Groups of similar code entities
- 📊 **Clone type** - exact_clone (98%+), very_similar (90%+), similar (80%+), conceptually_similar
- 📈 **Statistics** - Total clone groups, duplicate entities, estimated duplicate lines
- 💡 **Refactoring recommendations** - Actionable suggestions for each clone group
- ⚠️ **Priority** - critical, high, medium, low based on impact

### When to Use

✅ **For technical debt analysis:**
- Find copy-paste code
- Identify refactoring opportunities
- Estimate code duplication impact

✅ **For code quality improvement:**
- Consolidate duplicate logic
- Extract common patterns to utilities
- Reduce maintenance burden

✅ **For codebase understanding:**
- Discover recurring patterns
- Understand architectural inconsistencies
- Plan refactoring priorities

### Example Queries

```javascript
// Find exact duplicates
detect_code_clones(minSimilarity: 0.95)

// Find similar patterns for refactoring
detect_code_clones(minSimilarity: 0.75, maxGroups: 50)

// Comprehensive clone detection including classes
detect_code_clones(minSimilarity: 0.80, membersOnly: false)
```

### Best Practices

1. **Start with high similarity for duplicates:**
   ```javascript
   // Find true duplicates first
   detect_code_clones(minSimilarity: 0.9)
   ```

2. **Use lower thresholds for patterns:**
   ```javascript
   // Find similar implementations to consolidate
   detect_code_clones(minSimilarity: 0.7)
   ```

3. **Focus on critical clones first:**
   - Results are sorted by priority (critical → high → medium → low)
   - Start refactoring from highest priority groups
   - Consider estimated LOC saved

### Performance

- **Speed:** 10-30 sec depending on codebase size
- **Depends on:**
  - Number of entities to analyze
  - Embedding provider speed (TEI > Ollama > Memory)
  - maxGroups limit

### Related Tools

- ➡️ [**semantic_search**](#semantic_search) — find specific patterns
- ➡️ [**semantic_diff**](#semantic_diff) — verify refactoring preserved behavior
- ➡️ [**view_definition**](./ULTRA_SHARP_ANALYSIS.md#view_definition) — examine clone code
- ➡️ [**modify_code**](./ULTRA_SHARP_MODIFICATION.md#modify_code) — consolidate clones

---

## Workflow: Find and Consolidate Duplicates

```javascript
// 1. Detect all code clones in codebase
detect_code_clones(
    minSimilarity: 0.85,
    mode: "semantic",
    membersOnly: true,
    maxGroups: 20
)
// Output: Found 7 clone groups with duplicate email validation logic

// 2. Find specific validation duplicates
semantic_search(
    query: "validate email address format",
    scope: "solution",
    topK: 20,
    minSimilarity: 0.7
)
// Output: Found 7 similar implementations across different services

// 3. View each implementation
view_definition("UserService.ValidateEmail")
view_definition("EmailValidator.IsValidEmail")
view_definition("RegistrationService.CheckEmailFormat")
// ... etc

// 4. Choose best implementation
analyze_complexity(scope: "method", target: "EmailValidator.IsValidEmail")
// Output: Lowest complexity, well-tested

// 5. Update others to use best implementation
modify_code(
    fullyQualifiedMemberName: "UserService.ValidateEmail",
    newMemberCode: `
private bool ValidateEmail(string email)
{
    return EmailValidator.IsValidEmail(email);
}`,
    commitMessage: "Consolidate to EmailValidator.IsValidEmail"
)

// 6. Verify semantic equivalence
semantic_diff(
    beforeFqn: "UserService.ValidateEmail",  // old implementation
    afterFqn: "UserService.ValidateEmail",   // new implementation
    includeImplementationDetails: false
)
// Output: "Semantic similarity: 95%, behavior preserved, refactoring category"
```

---

## Workflow: Refactoring with Semantic Verification

```javascript
// 1. View current implementation
view_definition("OrderService.CalculateDiscount")
analyze_complexity(scope: "method", target: "OrderService.CalculateDiscount")
// Output: Complexity 25 - needs refactoring

// 2. Refactor to simpler implementation
modify_code(
    fullyQualifiedMemberName: "OrderService.CalculateDiscount",
    newMemberCode: `/* simplified implementation */`,
    commitMessage: "Simplify CalculateDiscount logic"
)

// 3. Verify semantic equivalence
semantic_diff(
    beforeFqn: "OrderService.CalculateDiscount",
    afterFqn: "OrderService.CalculateDiscount",
    includeImplementationDetails: true
)
// Output: "Semantic similarity: 98%, behavior preserved, refactoring category"
// codeMetrics: { beforeLines: 45, afterLines: 28, lineDelta: -17, sizeDeltaPercent: -37.78% }

// 4. Check new complexity
analyze_complexity(scope: "method", target: "OrderService.CalculateDiscount")
// Output: Complexity 12 - much better!

// 5. Run tests to verify
// dotnet test
```

---

## Setup Troubleshooting

### Common Issues

**1. "Semantic config not found"**
```
ERROR: Semantic configuration not found
```
**Solution:**
- Run setup: `setup-semantic-embedding.cmd` (Windows) or `pwsh Dev.Scripts/setup-semantic-embedding.ps1`
- Verify config exists: `Run.Config/semantic-config.json`

**2. "TEI service not running"**
```
ERROR: Cannot connect to TEI service
```
**Solution:**
- Start TEI Docker container: `pwsh Dev.Scripts/setup-tei.ps1`
- Check Docker is running: `docker ps`

**3. "Ollama not found"**
```
ERROR: Ollama not installed or not in PATH
```
**Solution:**
- Install Ollama from https://ollama.ai
- Pull embedding model: `ollama pull granite-embedding`

**4. "Validation failed"**
```
ERROR: Semantic configuration validation failed
```
**Solution:**
- Run validation: `pwsh Dev.Scripts/validate-semantic-config.ps1`
- Fix issues shown in validation output
- Re-run setup if needed

### Validation

**Check configuration:**
```bash
# Windows
validate-semantic-config.cmd

# Linux/macOS
pwsh Dev.Scripts/validate-semantic-config.ps1
```

**Expected output:**
```
✅ Semantic configuration valid
✅ Provider accessible
✅ Model available
✅ Test embedding successful
```

---

## Provider Comparison

| Provider | Performance | Setup | Offline | Best For |
|----------|------------|-------|---------|----------|
| **TEI** | ⚡⚡⚡ Fast | Docker | ✅ Yes | Production, large codebases |
| **Ollama** | ⚡⚡ Medium | Local install | ✅ Yes | Development, medium codebases |
| **Memory** | ⚡ Slow | None | ✅ Yes | Small codebases, testing |

---

## See Also

- 📚 [**ULTRA_SHARP_ANALYSIS.md**](./ULTRA_SHARP_ANALYSIS.md) — traditional code analysis
- 📚 [**ULTRA_SHARP_MODIFICATION.md**](./ULTRA_SHARP_MODIFICATION.md) — refactor found duplicates
- 📚 [**ULTRA_SHARP.md**](./ULTRA_SHARP.md) — main overview documentation
- 📂 **Setup Scripts:**
  - `Dev.Scripts/setup-semantic-embedding.ps1`
  - `Dev.Scripts/validate-semantic-config.ps1`
  - `Dev.Scripts/setup-tei.ps1`
