# Code Review - Complete Guide

**Automated code review: 5-10 minutes instead of 30-60 minutes manual work.**

---

## 🎯 The Problem

**Traditional code review approach (30-60 minutes):**
1. Manually read every changed file
2. Check for style inconsistencies
3. Look for complexity issues
4. Search for potential duplicates
5. Verify formatting compliance
6. Check for common mistakes
7. Write review comments
8. Wait for fixes and re-review

**MCP approach (5-10 minutes):**
1. `analyze_code_style` → Find all style issues (5 sec)
2. `analyze_complexity` → Identify complex code (5 sec)
3. `find_duplicates` → Check for duplicates (10 sec)
4. `format_code` → Verify formatting (3 sec)
5. `apply_code_fixes` → Auto-fix common issues (10 sec)

**Result:** 6-12x faster, more thorough, consistent quality.

---

## 🚀 Step-by-Step Workflow

### Step 1: Identify Changed Code

**Goal:** Understand scope of changes.

```bash
# Get list of changed files in feature branch
git diff --name-only dev...feature-branch

# Or get specific file changes
git diff dev...feature-branch -- src/
```

**What you need:**
- ✅ List of changed files
- ✅ Changed namespaces/types
- ✅ Scope of modifications

**Time:** 10-30 seconds.

---

### Step 2: Analyze Code Style

**Goal:** Find style violations before they enter codebase.

```csharp
analyze_code_style(
    path: "src/MyApp.NewFeature/",
    severityFilter: "Warning"
)
```

**What you get:**
```
Found 12 issues:

IDE0005: Using directive is unnecessary
  → UserService.cs:3
  → OrderService.cs:5

CS1998: Async method lacks 'await' operators
  → PaymentService.cs:45

CA1822: Member does not access instance data and can be marked as static
  → ValidationHelper.cs:67
  → ValidationHelper.cs:89

IDE0058: Expression value is never used
  → OrderController.cs:34
```

**Time:** 5-10 seconds vs 15-20 minutes manually.

**What you learn:**
- ✅ Unused imports
- ✅ Async/await issues
- ✅ Accessibility problems
- ✅ Naming violations
- ✅ Code smell patterns

---

### Step 3: Check Complexity

**Goal:** Identify overly complex code that needs refactoring.

```csharp
analyze_complexity(
    fullyQualifiedName: "MyApp.NewFeature.Services.OrderService",
    includeMembers: true
)
```

**Response example:**
```
OrderService (Maintainability Index: 72)

Members:
┌─────────────────────────────┬─────────┬───────────┬──────────┬─────────┐
│ Method                      │ Cyclo   │ Cognitive │ Coupling │ MI      │
├─────────────────────────────┼─────────┼───────────┼──────────┼─────────┤
│ CreateOrderAsync            │ 15 ⚠️    │ 18 ⚠️     │ 8        │ 65 ⚠️   │
│ ValidateOrderAsync          │ 8       │ 6         │ 4        │ 85 ✅   │
│ CalculateTotalAsync         │ 12 ⚠️    │ 14 ⚠️     │ 6        │ 70      │
│ ProcessPaymentAsync         │ 6       │ 5         │ 3        │ 88 ✅   │
└─────────────────────────────┴─────────┴───────────┴──────────┴─────────┘

⚠️ Issues found:
- CreateOrderAsync: High complexity (15), needs refactoring
- CalculateTotalAsync: High cognitive complexity (14)
```

**Time:** 5-10 seconds vs 20-30 minutes manually.

**Thresholds:**
- ✅ Cyclomatic < 10: Good
- ⚠️ Cyclomatic 10-15: Consider refactoring
- 🔴 Cyclomatic > 15: Requires refactoring

- ✅ Cognitive < 10: Good
- ⚠️ Cognitive 10-15: Consider simplification
- 🔴 Cognitive > 15: Requires simplification

- ✅ Maintainability Index > 80: Excellent
- ⚠️ MI 60-80: Acceptable
- 🔴 MI < 60: Poor, needs work

---

### Step 4: Find Duplicate Code

**Goal:** Ensure no code duplication introduced.

**Approach 1: Check for duplicates of new code**

```csharp
find_duplicates(
    targetCode: "async Task<bool> ValidateEmailAsync(string email) { return email.Contains('@') && email.Contains('.'); }",
    threshold: 0.85
)
```

**Results:**
```
Found 2 similar methods (threshold: 0.85):

1. UserValidator.IsEmailValid (similarity: 0.92)
   Location: MyApp.Core.Validators.UserValidator:23

2. RegistrationService.CheckEmailFormat (similarity: 0.88)
   Location: MyApp.Services.RegistrationService:56

⚠️ Duplication detected!
→ Consider consolidating into single shared method
```

**Approach 2: Check entire feature for internal duplicates**

```csharp
find_duplicates(
    targetCode: "async Task ProcessAsync() { try { await Operation(); } catch (Exception ex) { _logger.LogError(ex); throw; } }",
    threshold: 0.8
)
```

**Time:** 10-15 seconds vs 30-60 minutes manually.

---

### Step 5: Verify Formatting

**Goal:** Ensure code follows project formatting standards.

```csharp
format_code(
    path: "src/MyApp.NewFeature/",
    checkOnly: true
)
```

**Response (check mode):**
```
Formatting check completed

Files needing formatting: 3

src/MyApp.NewFeature/Services/OrderService.cs
  - Line 45: Inconsistent indentation
  - Line 67: Missing blank line before block

src/MyApp.NewFeature/Controllers/OrderController.cs
  - Line 23: Incorrect brace placement

src/MyApp.NewFeature/Models/OrderRequest.cs
  - Line 12: Extra blank lines
```

**Time:** 3-5 seconds vs 10-15 minutes manually.

**Next step:** Apply formatting

```csharp
format_code(
    path: "src/MyApp.NewFeature/",
    checkOnly: false
)
```

**Response (apply mode):**
```
✅ Formatted 3 files
✅ All files now compliant with CSharpier standards
```

---

### Step 6: Apply Auto-Fixes

**Goal:** Automatically fix common issues found in Step 2.

**Approach 1: Preview fixes first**

```csharp
apply_code_fixes(
    diagnosticId: "all",
    preview: true
)
```

**Response:**
```
Preview: 8 fixes available

IDE0005: Remove unnecessary using
  → UserService.cs:3: using System.Linq; (unused)
  → OrderService.cs:5: using System.Collections; (unused)

CS1998: Add 'await' or remove 'async'
  → PaymentService.cs:45: public async Task ProcessRefund()
    Suggestion: Remove 'async' keyword (no await in method)

CA1822: Make method static
  → ValidationHelper.cs:67: public bool IsValid()
  → ValidationHelper.cs:89: public string Format()
```

**Approach 2: Apply fixes**

```csharp
apply_code_fixes(
    diagnosticId: "all",
    preview: false
)
```

**Response:**
```
✅ Applied 8 fixes across 4 files
✅ All auto-fixable issues resolved

Modified files:
- src/MyApp.NewFeature/Services/UserService.cs
- src/MyApp.NewFeature/Services/OrderService.cs
- src/MyApp.NewFeature/Services/PaymentService.cs
- src/MyApp.NewFeature/Helpers/ValidationHelper.cs
```

**Time:** 10-15 seconds vs 30-45 minutes manually fixing.

---

### Step 7: Final Quality Check

**Goal:** Verify all issues resolved.

```csharp
// Re-run style analysis
analyze_code_style(
    path: "src/MyApp.NewFeature/",
    severityFilter: "Warning"
)

// Re-check formatting
format_code(
    path: "src/MyApp.NewFeature/",
    checkOnly: true
)
```

**Expected result:**
```
AnalyzeCodeStyle:
✅ No issues found

format_code:
✅ All files correctly formatted
```

**Time:** 5 seconds.

---

## 📋 Complete Code Review Template

**Use this for every feature branch:**

```
1. AnalyzeCodeStyle(path: "src/FeaturePath/", severityFilter: "Warning")
   → Find style violations

2. AnalyzeComplexity(fullyQualifiedName: "Feature.MainClass", includeMembers: true)
   → Identify complex methods

3. FindPotentialDuplicates(targetCode: "new method signature", threshold: 0.85)
   → Check for duplicates

4. FormatCode(path: "src/FeaturePath/", checkOnly: true)
   → Verify formatting compliance

5. ApplyCodeFixes(diagnosticId: "all", preview: true)
   → See available fixes

6. ApplyCodeFixes(diagnosticId: "all", preview: false)
   → Apply fixes

7.format_codee(path: "src/FeaturePath/", checkOnly: false)
   → Apply formatting

8. AnalyzeCodeStyle(path: "src/FeaturePath/", severityFilter: "Warning")
   → Final verification
```

**Total time:** 5-10 minutes for complete review.

---

## 🎯 Real-World Example: Payment Feature Review

**Scenario:** Developer submitted PR for new payment feature. Need to review before merge.

### Minutes 0-1: Style Analysis

```csharp
AnalyzeCodeStyle(
    path: "src/MyApp.Payment/",
    severityFilter: "Warning"
)
```

**Results:**
```
Found 15 issues:
- 5x IDE0005 (unused usings)
- 3x CS1998 (async without await)
- 4x CA1822 (can be static)
- 2x IDE0058 (unused expression)
- 1x CA2007 (missing ConfigureAwait)
```

**Understanding:** Several common issues, all auto-fixable.

---

### Minutes 1-2: Complexity Check

```csharp
AnalyzeComplexity(
    fullyQualifiedName: "MyApp.Payment.Services.PaymentProcessor",
    includeMembers: true
)
```

**Results:**
```
PaymentProcessor (MI: 68 ⚠️)

┌──────────────────────────────┬─────────┬───────────┬─────────┐
│ Method                       │ Cyclo   │ Cognitive │ MI      │
├──────────────────────────────┼─────────┼───────────┼─────────┤
│ ProcessPaymentAsync          │ 18 🔴   │ 22 🔴     │ 58 🔴   │
│ ValidatePaymentAsync         │ 8       │ 6         │ 85 ✅   │
│ RefundPaymentAsync           │ 6       │ 5         │ 88 ✅   │
└──────────────────────────────┴─────────┴───────────┴─────────┘

🔴 Critical: ProcessPaymentAsync is too complex!
```

**Action required:** Request refactoring before merge.

---

### Minutes 2-3: Check for Duplicates

```csharp
FindPotentialDuplicates(
    targetCode: "async Task<PaymentResult> ValidatePaymentAsync(PaymentRequest request) { if (request.Amount <= 0) return PaymentResult.Failed('Invalid amount'); }",
    threshold: 0.85
)
```

**Results:**
```
Found 1 similar method:

OrderService.ValidateOrderAmount (similarity: 0.89)
  → Similar validation logic exists
  → Consider extracting to shared validator
```

**Issue identified:** Duplicate validation logic.

---

### Minutes 3-4: Format Check

```csharp
FormatCode(
    path: "src/MyApp.Payment/",
    checkOnly: true
)
```

**Results:**
```
Files needing formatting: 7

⚠️ Multiple formatting issues
→ Need to run formatter
```

---

### Minutes 4-5: Apply Auto-Fixes

```csharp
// Preview first
ApplyCodeFixes(diagnosticId: "all", preview: true)

// Then apply
ApplyCodeFixes(diagnosticId: "all", preview: false)
```

**Results:**
```
✅ Fixed 15 issues automatically
```

---

### Minutes 5-6: Apply Formatting

```csharp
FormatCode(
    path: "src/MyApp.Payment/",
    checkOnly: false
)
```

**Results:**
```
✅ Formatted 7 files
```

---

### Minutes 6-7: Final Verification

```csharp
// Check style again
AnalyzeCodeStyle(
    path: "src/MyApp.Payment/",
    severityFilter: "Warning"
)

// Check formatting again
FormatCode(
    path: "src/MyApp.Payment/",
    checkOnly: true
)
```

**Results:**
```
AnalyzeCodeStyle: ✅ No issues
FormatCode: ✅ All files formatted correctly
```

---

### Review Summary

**Total time:** 7 minutes

**Issues found:**
1. 🔴 **Critical:** ProcessPaymentAsync too complex (CC: 18, Cognitive: 22, MI: 58)
   - **Action:** Request refactoring (extract methods)

2. ⚠️ **Minor:** Duplicate validation logic with OrderService
   - **Action:** Suggest extracting shared validator

3. ✅ **Auto-fixed:** 15 style issues
4. ✅ **Auto-fixed:** 7 formatting issues

**Review comment:**
```markdown
## Code Review - Payment Feature

**Quality Issues:**
- 🔴 `PaymentProcessor.ProcessPaymentAsync` is too complex (CC: 18, MI: 58)
  - Please extract smaller methods (validation, processing, logging)
  - Target: CC < 10, MI > 80

- ⚠️ Duplicate validation logic found in `OrderService.ValidateOrderAmount`
  - Consider extracting to `PaymentValidationService`

**Auto-Fixed:**
- ✅ Removed 5 unused using directives
- ✅ Fixed 3 async/await issues
- ✅ Made 4 methods static
- ✅ Fixed 2 unused expressions
- ✅ Added ConfigureAwait(false)
- ✅ Formatted all files

**Status:** Requires changes before merge.
```

**Manual review time:** 60-90 minutes
**MCP review time:** 7 minutes
**Speedup:** 8-13x

---

## 💡 Best Practices

### 1. Review in Order

```csharp
✅ CORRECT ORDER:
1. AnalyzeCodeStyle → Find issues
2. AnalyzeComplexity → Identify problems
3. FindPotentialDuplicates → Check duplicates
4. ApplyCodeFixes → Auto-fix
5. FormatCode → Apply formatting
6. Re-check → Verify

❌ WRONG ORDER:
1. FormatCode first → May format code with issues
2. Skip complexity check → Miss critical problems
```

### 2. Set Severity Thresholds

```csharp
// For new features: strict
AnalyzeCodeStyle(severityFilter: "Warning")

// For bug fixes: medium
AnalyzeCodeStyle(severityFilter: "Error")

// For experiments: lenient
AnalyzeCodeStyle(severityFilter: "Error")
```

### 3. Check Complexity Thresholds

```csharp
✅ Accept:
- Cyclomatic < 10
- Cognitive < 10
- MI > 80

⚠️ Request improvements:
- Cyclomatic 10-15
- Cognitive 10-15
- MI 60-80

🔴 Block merge:
- Cyclomatic > 15
- Cognitive > 15
- MI < 60
```

### 4. Use Preview Mode First

```csharp
// Always preview before applying
ApplyCodeFixes(diagnosticId: "all", preview: true)
→ Review changes

// Then apply if looks good
ApplyCodeFixes(diagnosticId: "all", preview: false)
```

### 5. Document Review Results

```markdown
## Code Review - [Feature Name]

**Complexity:**
- ✅/⚠️/🔴 [Class.Method]: CC=X, Cognitive=Y, MI=Z

**Duplicates:**
- ✅ No duplicates found
- OR ⚠️ [Location]: Similar to [Other Location]

**Style:**
- ✅ No issues after auto-fix
- OR ⚠️ [Manual fixes needed]

**Formatting:**
- ✅ All files formatted correctly

**Status:** ✅ Approved / ⚠️ Needs improvements / 🔴 Requires changes
```

---

## ⚠️ Common Pitfalls

### Pitfall 1: Skipping Complexity Check

```csharp
❌ WRONG:
analyze_code_style(...) → looks good → approve
// Miss complex methods!

✅ RIGHT:
AnalyzeCodeStyle(...)
AnalyzeComplexity(...) // Don't skip this!
→ Now safe to approve
```

### Pitfall 2: Not Checking for Duplicates

```csharp
❌ WRONG:
"Code looks clean, no obvious issues"
// Miss duplicate logic across files

✅ RIGHT:
find_duplicates(targetCode: "new method", threshold: 0.85)
→ Find hidden duplicates
```

### Pitfall 3: Applying Fixes Without Preview

```csharp
❌ WRONG:
apply_code_fixes(diagnosticId: "all", preview: false)
// Might apply unwanted fixes

✅ RIGHT:
ApplyCodeFixes(diagnosticId: "all", preview: true)
→ Review fixes
→ Then apply if acceptable
```

### Pitfall 4: Ignoring Complexity Metrics

```csharp
❌ WRONG: "MI is 55, but code works, so it's fine"
→ Technical debt accumulates

✅ RIGHT: "MI < 60 → request refactoring before merge"
→ Maintain code quality
```

### Pitfall 5: Not Re-Checking After Fixes

```csharp
❌ WRONG:
ApplyCodeFixes(...) → done!
// No verification

✅ RIGHT:
ApplyCodeFixes(...)
→ AnalyzeCodeStyle(...) // Verify
→ FormatCode(checkOnly: true) // Verify
→ Now truly done
```

---

## 📊 Time Comparison

| Task | Manual | With MCP | Speedup |
|------|--------|----------|---------|
| Style check | 15-20 min | 5 sec | **180-240x** |
| Complexity analysis | 20-30 min | 5 sec | **240-360x** |
| Find duplicates | 30-60 min | 10 sec | **180-360x** |
| Format check | 10-15 min | 3 sec | **200-300x** |
| Apply fixes | 30-45 min | 10 sec | **180-270x** |
| **TOTAL** | **60-90 min** | **5-10 min** | **6-18x** |

---

## 🎓 Advanced Techniques

### Technique 1: Multi-Project Review

**Review changes across multiple projects:**

```csharp
// Review Core project
AnalyzeCodeStyle(path: "src/MyApp.Core/", severityFilter: "Warning")
AnalyzeComplexity(fullyQualifiedName: "MyApp.Core.NewFeature")

// Review API project
AnalyzeCodeStyle(path: "src/MyApp.API/", severityFilter: "Warning")
AnalyzeComplexity(fullyQualifiedName: "MyApp.API.Controllers.NewController")

// Review Infrastructure project
AnalyzeCodeStyle(path: "src/MyApp.Infrastructure/", severityFilter: "Warning")
```

### Technique 2: Focused Complexity Review

**Check only changed methods:**

```csharp
// Get list of changed methods from git diff
// Then check each:

analyze_complexity(
    fullyQualifiedName: "MyApp.Services.UserService.CreateUserAsync"
)

AnalyzeComplexity(
    fullyQualifiedName: "MyApp.Services.OrderService.ProcessOrderAsync"
)
```

### Technique 3: Duplicate Detection Across Feature

**Find all similar patterns in new feature:**

```csharp
// Pattern 1: Validation
FindPotefind_duplicatesrgetCode: "if (x == null) throw new ArgumentNullException();",
    threshold: 0.8
)

// Pattern 2: Error handling
FindPotentialDuplicates(
    targetCode: "try { } catch (Exception ex) { _logger.LogError(ex); throw; }",
    threshold: 0.75
)

// Pattern 3: Database operations
FindPotentialDuplicates(
    targetCode: "await _context.SaveChangesAsync();",
    threshold: 0.85
)
```

### Technique 4: Progressive Quality Gates

**Implement quality gates by severity:**

```csharp
// Gate 1: Block if errors exist
AnalyzeCodeStyle(severityFilter: "Error")
// → Must be zero errors

// Gate 2: Block if too many warninganalyze_code_stylele(severityFilter: "Warning")
// → Must be < 5 warnings

// Gate 3: Block if complexity too highanalyze_complexityy(includeMembers: true)
// → No method with CC > 15
```

---

## 🔄 Automated Review Workflow

**Can be scripted:**

```powershell
# review-feature.ps1
param(
    [string]$FeaturePath,
    [string]$FeatureClass
)

Write-Host "Starting code review..."

# Step 1: Style
Write-Host "1. Checking style..."
$styleIssues = mcp-call AnalyzeCodeStyle -path $FeaturePath -severityFilter "Warning"

# Step 2: Complexity
Write-Host "2. Checking complexity..."
$complexity = mcp-call AnalyzeComplexity -fullyQualifiedName $FeatureClass -includeMembers $true

# Step 3: Auto-fix
Write-Host "3. Applying auto-fixes..."
mcp-call ApplyCodeFixes -diagnosticId "all" -preview $false

# Step 4: Format
Write-Host "4. Formatting code..."
mcp-call FormatCode -path $FeaturePath -checkOnly $false

# Step 5: Verify
Write-Host "5. Final verification..."
$finalCheck = mcp-canalyze_code_styletyle -path $FeaturePath -severityFilter "Warning"

if ($finalCheck -eq "No issues") {
    Write-Host "✅ Review passed!"
} else {
    Write-Host "⚠️ Issues remain, manual review needed"
}
```

---

**💡 Key Takeaway:** 5-10 minutes automated review is more thorough than 60-90 minutes manual review.

**Always run full review workflow before approving any PR.**
