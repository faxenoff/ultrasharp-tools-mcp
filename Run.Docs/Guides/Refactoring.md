# Refactoring Code - Complete Guide

**Improve code quality in 15-20 minutes instead of 2-3 hours.**

---

## 🎯 The Problem

**Typical scenario:** Code has accumulated duplicates, high complexity, style inconsistencies.

**Manual approach (2-3 hours):**
1. Browse through files looking for duplicates
2. Manually calculate complexity (if at all)
3. Guess which code needs refactoring
4. Make changes, hope compilation works
5. Manually check style issues
6. Miss many improvement opportunities

**MCP approach (15-20 minutes):**
1. `FindPotentialDuplicates` → find all duplicates automatically
2. `AnalyzeComplexity` → identify high-complexity code
3. `OverwriteMember` → refactor with auto-linting
4. `FormatCode` → consistent style
5. `ApplyCodeFixes` → auto-fix warnings
6. `AnalyzeCodeStyle` → verify improvements

**Result:** 6-12x faster, more thorough, measurable improvements.

---

## 🛠️ Available Tools

### Detection Tools

**FindPotentialDuplicates** - Semantic duplicate detection
- Finds duplicates by meaning, not text
- Threshold: 0.8-0.9 for refactoring candidates
- Works across different naming conventions

**AnalyzeComplexity** - Complexity metrics
- Cyclomatic complexity (branches)
- Cognitive complexity (understandability)
- Coupling (afferent/efferent)
- Maintainability index

**AnalyzeCodeStyle** - Roslyn analyzers
- IDE/CS/CA rules
- Filter by severity
- Shows fixable issues

### Refactoring Tools

**OverwriteMember** - Modify code
- Replace method/class implementation
- Auto-linting included!
- Git commit automatic

**RenameSymbol** - Safe renaming
- Renames symbol + all references
- Across entire solution
- No broken references

**MoveMember** - Move code between types
- Move methods/properties
- Updates all references
- Maintains structure

### Quality Tools

**FormatCode** - Consistent style
- CSharpier integration
- Check/apply modes
- Parallel processing

**ApplyCodeFixes** - Auto-fix issues
- IDE0005 (unused usings)
- CS8019, IDE0028, etc.
- Preview/apply modes

---

## 🚀 Step-by-Step Workflow

### Step 1: Find Duplicates

**Goal:** Identify duplicate or similar code.

```csharp
UltrasharpTool_FindPotentialDuplicates(
    targetCode: @"
bool ValidateEmail(string email)
{
    return email.Contains('@') && email.Contains('.');
}
",
    threshold: 0.8
)
```

**Results:**
```
Found 5 duplicates/similar methods (threshold: 0.8):

1. UserValidator.ValidateEmailFormat (similarity: 0.95)
   Location: Validators/UserValidator.cs:45
   public bool ValidateEmailFormat(string emailAddress) {
       return emailAddress.Contains("@") && emailAddress.Contains(".");
   }

2. RegistrationService.CheckEmail (similarity: 0.89)
   Location: Services/RegistrationService.cs:78
   private bool CheckEmail(string mail) {
       var hasAt = mail.Contains('@');
       var hasDot = mail.Contains('.');
       return hasAt && hasDot;
   }

3. EmailHelper.IsValidEmail (similarity: 0.87)
   Location: Utilities/EmailHelper.cs:23
   public static bool IsValidEmail(string input) {
       return input.IndexOf('@') > 0 && input.IndexOf('.') > input.IndexOf('@');
   }

4. AccountController.ValidateUserEmail (similarity: 0.84)
   Location: Controllers/AccountController.cs:156
   private bool ValidateUserEmail(string email) {
       if (string.IsNullOrEmpty(email)) return false;
       return email.Contains("@") && email.Contains(".");
   }

5. ProfileService.IsEmailValid (similarity: 0.82)
   Location: Services/ProfileService.cs:234
   bool IsEmailValid(string e) {
       return !string.IsNullOrEmpty(e) && e.Contains('@') && e.Contains('.');
   }

💡 Refactoring opportunity: Consolidate into single validation utility
```

**Time:** 5-10 seconds vs 30-60 minutes manual search.

---

### Step 2: Analyze Complexity

**Goal:** Find complex code that needs simplification.

```csharp
UltrasharpTool_AnalyzeComplexity(
    fullyQualifiedName: "MyApp.Services",
    includeMembers: true
)
```

**Results:**
```
📊 Complexity Analysis: MyApp.Services

╔═══════════════════════════════════════════════════════════╗
║ HIGH COMPLEXITY METHODS (Cyclomatic > 15)                  ║
╚═══════════════════════════════════════════════════════════╝

1. OrderService.ProcessOrder
   Cyclomatic: 28  ⚠️ TOO HIGH (threshold: 15)
   Cognitive: 42   ⚠️ TOO HIGH (threshold: 20)
   Lines: 156
   → Recommendation: Split into smaller methods

2. PaymentProcessor.ChargeCard
   Cyclomatic: 19  ⚠️ HIGH
   Cognitive: 31   ⚠️ TOO HIGH
   Lines: 89
   → Recommendation: Extract validation logic

3. UserService.UpdateUserProfile
   Cyclomatic: 17  ⚠️ HIGH
   Cognitive: 24   ⚠️ HIGH
   Lines: 67
   → Recommendation: Extract permission checks

╔═══════════════════════════════════════════════════════════╗
║ HIGH COUPLING (Afferent + Efferent > 10)                   ║
╚═══════════════════════════════════════════════════════════╝

1. OrderService
   Afferent: 8 (8 types depend on this)
   Efferent: 12 (depends on 12 types)
   Total coupling: 20  ⚠️ HIGH
   → Recommendation: Consider splitting responsibilities

2. UserService
   Afferent: 12 (many dependents - good)
   Efferent: 9 (many dependencies - concerning)
   Total coupling: 21  ⚠️ HIGH
   → Recommendation: Review dependencies

╔═══════════════════════════════════════════════════════════╗
║ SUMMARY                                                     ║
╚═══════════════════════════════════════════════════════════╝

Total methods analyzed: 47
High complexity: 3 (6%)
Medium complexity: 12 (26%)
Low complexity: 32 (68%)

Maintainability Index (average): 64/100 (Fair)
→ Target: 80+ (Good), 90+ (Excellent)
```

**Time:** 10-15 seconds vs impossible manually (nobody calculates this).

---

### Step 3: Refactor High Complexity Method

**Goal:** Simplify OrderService.ProcessOrder (cyclomatic: 28 → <10).

**Original method (via ViewDefinition):**
```csharp
public async Task<OrderResult> ProcessOrder(CreateOrderRequest request)
{
    // 156 lines of spaghetti code with:
    // - 8 nested if statements
    // - 5 try-catch blocks
    // - 3 loops
    // - Validation + business logic + db access all mixed
}
```

**Refactoring strategy:**
1. Extract validation → ValidateOrder()
2. Extract pricing → CalculatePricing()
3. Extract inventory check → CheckInventory()
4. Extract payment → ProcessPayment()
5. Extract notifications → SendNotifications()

**Create simplified version:**
```csharp
UltrasharpTool_OverwriteMember(
    fullyQualifiedTargetName: "MyApp.Services.OrderService.ProcessOrder",
    codeSnippet: @"
public async Task<OrderResult> ProcessOrder(CreateOrderRequest request)
{
    // Validate
    var validationResult = await ValidateOrder(request);
    if (!validationResult.IsValid)
        return OrderResult.ValidationFailed(validationResult.Errors);

    // Check inventory
    var inventoryResult = await CheckInventory(request.Items);
    if (!inventoryResult.Available)
        return OrderResult.OutOfStock(inventoryResult.UnavailableItems);

    // Calculate pricing
    var pricing = await CalculatePricing(request.Items, request.DiscountCode);

    // Process payment
    var paymentResult = await ProcessPayment(pricing.Total, request.PaymentMethod);
    if (!paymentResult.IsSuccess)
        return OrderResult.PaymentFailed(paymentResult.ErrorMessage);

    // Create order
    var order = await _orderRepository.CreateAsync(new Order
    {
        Items = request.Items,
        Total = pricing.Total,
        PaymentId = paymentResult.TransactionId,
        Status = OrderStatus.Confirmed
    });

    // Send notifications
    await SendNotifications(order);

    return OrderResult.Success(order.Id);
}
",
    commitMessage: "Refactor ProcessOrder: extract methods to reduce complexity (28 → 6 cyclomatic)"
)
```

**Response:**
```
✅ Member overwritten successfully

📊 Code Quality Check:
✅ No compilation errors
⚠️ 2 warnings:
   - IDE0051: ValidateOrder is not used elsewhere
   - IDE0051: CalculatePricing is not used elsewhere
   → This is OK - private helper methods

🌳 Git:
Branch: ultrasharptools/20250117-153022
Commit: b8d4f21 "Refactor ProcessOrder: extract methods to reduce complexity"

💡 Next steps:
   - Implement extracted methods (ValidateOrder, CalculatePricing, etc.)
   - Run FormatCode
   - Run AnalyzeComplexity again to verify improvement
```

---

### Step 4: Implement Extracted Methods

**Create each helper method:**

```csharp
UltrasharpTool_AddMember(
    fullyQualifiedTargetName: "MyApp.Services.OrderService",
    codeSnippet: @"
private async Task<ValidationResult> ValidateOrder(CreateOrderRequest request)
{
    if (request == null)
        return ValidationResult.Failed(""Request is null"");

    if (request.Items == null || !request.Items.Any())
        return ValidationResult.Failed(""No items in order"");

    if (request.Items.Any(i => i.Quantity <= 0))
        return ValidationResult.Failed(""Invalid quantity"");

    return ValidationResult.Success();
}
",
    commitMessage: "Add ValidateOrder helper method"
)

// Repeat for: CalculatePricing, CheckInventory, ProcessPayment, SendNotifications
```

**Result:** Cyclomatic complexity: 28 → 6 (78% reduction!)

---

### Step 5: Consolidate Duplicates

**Goal:** Replace 5 duplicate email validations with single utility.

**Create shared validation utility:**
```csharp
UltrasharpTool_AddMember(
    fullyQualifiedTargetName: "MyApp.Utilities.EmailValidator",
    codeSnippet: @"
/// <summary>
/// Validates email address format
/// </summary>
/// <param name=""email"">Email address to validate</param>
/// <returns>True if email is valid, false otherwise</returns>
public static bool IsValidEmail(string email)
{
    if (string.IsNullOrWhiteSpace(email))
        return false;

    // Basic validation: must contain @ and .
    // More sophisticated validation could use Regex
    return email.Contains('@') &&
           email.Contains('.') &&
           email.IndexOf('@') > 0 &&
           email.IndexOf('.') > email.IndexOf('@');
}
",
    commitMessage: "Add centralized email validation utility"
)
```

**Replace all duplicates:**
```csharp
// Replace UserValidator.ValidateEmailFormat
UltrasharpTool_OverwriteMember(
    fullyQualifiedTargetName: "MyApp.Validators.UserValidator.ValidateEmailFormat",
    codeSnippet: "public bool ValidateEmailFormat(string email) => EmailValidator.IsValidEmail(email);"
)

// Replace RegistrationService.CheckEmail
UltrasharpTool_OverwriteMember(
    fullyQualifiedTargetName: "MyApp.Services.RegistrationService.CheckEmail",
    codeSnippet: "private bool CheckEmail(string email) => EmailValidator.IsValidEmail(email);"
)

// Or better: use RenameSymbol to update all references
UltrasharpTool_FindReferences(
    fullyQualifiedName: "UserValidator.ValidateEmailFormat"
)
// Then replace call sites directly
```

**Result:** 5 duplicate implementations → 1 centralized utility.

---

### Step 6: Format Code

**Goal:** Ensure consistent code style.

```csharp
UltrasharpTool_FormatCode(
    path: "D:/Projects/MyApp/MyApp.Services",
    checkOnly: true
)
```

**Results:**
```
Format Check: MyApp.Services

Files needing formatting: 8
- OrderService.cs (inconsistent indentation)
- PaymentProcessor.cs (trailing whitespace)
- UserService.cs (brace placement)
... (5 more)

💡 Run with checkOnly: false to apply formatting
```

**Apply formatting:**
```csharp
UltrasharpTool_FormatCode(
    path: "D:/Projects/MyApp/MyApp.Services",
    checkOnly: false
)
```

**Results:**
```
✅ Formatting applied

Files formatted: 8
Files unchanged: 23

🌳 Git:
Branch: ultrasharptools/20250117-153245
Commit: c9e8a42 "Format code with CSharpier (8 files)"
```

---

### Step 7: Analyze & Fix Code Style

**Goal:** Find and fix remaining issues.

```csharp
UltrasharpTool_AnalyzeCodeStyle(
    solutionPath: "D:/Projects/MyApp/MyApp.sln",
    severityFilter: "Warning"
)
```

**Results:**
```
Code Style Analysis

Total diagnostics: 87
Errors: 0  ✅
Warnings: 43
Info: 44

Top issues:
1. IDE0005: Unnecessary using directive (18 occurrences)
2. CS8019: Unnecessary using directive (12 occurrences)
3. IDE0028: Collection initialization can be simplified (7 occurrences)
4. CA1031: Do not catch general exception types (6 occurrences)

Auto-fixable: 37 warnings
Manual fixes required: 6 warnings
```

**Auto-fix:**
```csharp
UltrasharpTool_ApplyCodeFixes(
    solutionPath: "D:/Projects/MyApp/MyApp.sln",
    diagnosticId: "all",
    preview: true
)
```

**Preview:**
```
Code Fixes Preview

Will fix 37 issues across 15 files:
- Remove 30 unnecessary usings
- Simplify 7 collection initializations

Files affected:
- Services/OrderService.cs (8 fixes)
- Services/UserService.cs (6 fixes)
... (13 more files)
```

**Apply:**
```csharp
UltrasharpTool_ApplyCodeFixes(
    solutionPath: "D:/Projects/MyApp/MyApp.sln",
    diagnosticId: "all",
    preview: false
)
```

**Results:**
```
✅ Code fixes applied

Files modified: 15
Total fixes: 37

🌳 Git:
Branch: ultrasharptools/20250117-153310
Commit: d7f9b32 "Auto-fix code style issues (37 fixes in 15 files)"

Remaining warnings: 6 (manual fixes required)
```

---

### Step 8: Verify Improvements

**Goal:** Measure improvement after refactoring.

```csharp
UltrasharpTool_AnalyzeComplexity(
    fullyQualifiedName: "MyApp.Services",
    includeMembers: true
)
```

**Results:**
```
📊 Complexity Analysis (After Refactoring)

BEFORE → AFTER:

OrderService.ProcessOrder
   Cyclomatic: 28 → 6  ✅ (-78%)
   Cognitive: 42 → 8   ✅ (-81%)
   Lines: 156 → 35     ✅ (-78%)

PaymentProcessor.ChargeCard
   Cyclomatic: 19 → 12  ✅ (-37%)
   Cognitive: 31 → 18   ✅ (-42%)

UserService.UpdateUserProfile
   Cyclomatic: 17 → 9   ✅ (-47%)
   Cognitive: 24 → 11   ✅ (-54%)

SUMMARY:
Total methods: 47 → 53 (+6 extracted helpers)
High complexity: 3 → 0  ✅ (-100%)
Medium complexity: 12 → 8  ✅ (-33%)

Maintainability Index: 64 → 82  ✅ (+28%)
   Was: Fair
   Now: Good

💡 Excellent improvement! All high-complexity methods resolved.
```

**Code style verification:**
```csharp
UltrasharpTool_AnalyzeCodeStyle(
    solutionPath: "D:/Projects/MyApp/MyApp.sln",
    severityFilter: "Warning"
)
```

**Results:**
```
Total warnings: 43 → 6  ✅ (-86%)
Auto-fixed: 37 ✅
Remaining: 6 (manual review needed)
```

---

## 📋 Complete Workflow Template

**Use this for systematic refactoring:**

```
1. FindPotentialDuplicates(targetCode: "...", threshold: 0.8)
   → Identify duplicate code

2. AnalyzeComplexity(fullyQualifiedName: "...", includeMembers: true)
   → Find high-complexity methods

3. ViewDefinition(fullyQualifiedName: "HighComplexityMethod")
   → Understand what needs refactoring

4. OverwriteMember(...)
   → Refactor: extract methods, simplify logic
   → Consolidate duplicates

5. FormatCode(path: "...", checkOnly: false)
   → Consistent formatting

6. AnalyzeCodeStyle(severityFilter: "Warning")
   → Find remaining issues

7. ApplyCodeFixes(diagnosticId: "all", preview: false)
   → Auto-fix warnings

8. AnalyzeComplexity(...) + AnalyzeCodeStyle(...)
   → Verify improvements
```

**Total time:** 15-30 minutes vs 2-4 hours manually.

---

## 💡 Best Practices

### 1. Start with Metrics

```csharp
✅ BEFORE refactoring:
AnalyzeComplexity(...)  → Baseline: cyclomatic=28
AnalyzeCodeStyle(...)   → Baseline: 43 warnings

✅ AFTER refactoring:
AnalyzeComplexity(...)  → Result: cyclomatic=6 (-78%)
AnalyzeCodeStyle(...)   → Result: 6 warnings (-86%)

→ Measurable improvement!
```

### 2. Refactor One Thing at a Time

```csharp
❌ BAD: Refactor complexity + duplicates + style all at once
   → Hard to track, easy to break

✅ GOOD:
   1. Fix duplicates first
   2. Then reduce complexity
   3. Then fix style
   → Commit after each step
```

### 3. Use Semantic Search for Duplicates

```csharp
// Don't search for exact text matches
❌ grep -r "email.Contains('@')" .

// Use semantic search
✅ FindPotentialDuplicates(
     targetCode: "validate email with @ and .",
     threshold: 0.8
   )
→ Finds duplicates with different implementations
```

### 4. Trust Complexity Metrics

```csharp
// High cyclomatic complexity = needs refactoring
Cyclomatic > 15  ⚠️ HIGH - definitely refactor
Cyclomatic > 10  ⚠️ MEDIUM - consider refactoring
Cyclomatic < 10  ✅ GOOD

// High cognitive complexity = hard to understand
Cognitive > 20   ⚠️ TOO HIGH
Cognitive > 15   ⚠️ HIGH
Cognitive < 10   ✅ GOOD
```

### 5. Always Verify Improvements

```csharp
// Before
var before = AnalyzeComplexity(...);

// Refactor
OverwriteMember(...);

// After
var after = AnalyzeComplexity(...);

// Compare
Console.WriteLine($"Complexity: {before.Cyclomatic} → {after.Cyclomatic}");
→ Quantifiable improvement!
```

---

## 🎯 Real-World Example: Legacy Service Cleanup

**Scenario:** Legacy OrderService with 200+ lines, cyclomatic=35, 12 duplicates.

### Minutes 0-3: Analyze Current State

```csharp
// Complexity
AnalyzeComplexity(
    fullyQualifiedName: "LegacyApp.Services.OrderService",
    includeMembers: true
)

// Results:
// ProcessOrder: Cyclomatic=35, Cognitive=58, Lines=234

// Find duplicates
FindPotentialDuplicates(
    targetCode: "validate user permissions for order access",
    threshold: 0.75
)

// Results: 4 duplicate permission checks
// Results: 3 duplicate validation methods
// Results: 5 duplicate logging patterns
```

---

### Minutes 3-10: Extract Methods

```csharp
// Extract: ValidateOrder, CheckPermissions, CalculatePricing,
//          ProcessPayment, UpdateInventory, SendNotifications

OverwriteMember(
    fullyQualifiedTargetName: "OrderService.ProcessOrder",
    codeSnippet: "/* simplified with extracted methods */"
)

// Result: 234 lines → 45 lines
// Result: Cyclomatic 35 → 7
```

---

### Minutes 10-15: Consolidate Duplicates

```csharp
// Create shared PermissionChecker utility
AddMember(
    fullyQualifiedTargetName: "Utilities.PermissionChecker",
    codeSnippet: "public static bool CanAccessOrder(User user, Order order) { ... }"
)

// Replace 4 duplicate permission checks
OverwriteMember(...)  // × 4 locations

// Result: 4 duplicates → 1 utility
```

---

### Minutes 15-20: Quality Cleanup

```csharp
// Format
FormatCode(path: "Services/", checkOnly: false)

// Analyze
AnalyzeCodeStyle(severityFilter: "Warning")
// Result: 28 warnings

// Auto-fix
ApplyCodeFixes(diagnosticId: "all", preview: false)
// Result: 24 fixed, 4 remain
```

---

**Total time:** 20 minutes

**Results:**
- Lines: 234 → 85 (-64%)
- Cyclomatic: 35 → 7 (-80%)
- Cognitive: 58 → 12 (-79%)
- Duplicates: 12 → 0 (-100%)
- Warnings: 28 → 4 (-86%)
- Maintainability: 42 → 78 (+86%)

**Manual effort:** Would take 4-6 hours + miss many duplicates.

---

## ⚠️ Common Refactoring Patterns

### Pattern 1: God Method

**Before:**
```csharp
// 300 lines, does everything
public void ProcessEverything() {
    // validation
    // business logic
    // database access
    // email sending
    // logging
    // error handling
}
```

**After:**
```csharp
public void ProcessEverything() {
    Validate();
    ProcessBusinessLogic();
    SaveToDatabase();
    SendNotifications();
}
```

---

### Pattern 2: Nested Conditionals

**Before:**
```csharp
if (user != null) {
    if (user.IsActive) {
        if (user.HasPermission("Edit")) {
            if (order != null) {
                if (order.Status == "Pending") {
                    // actual logic
                }
            }
        }
    }
}
```

**After:**
```csharp
if (user == null || !user.IsActive) return;
if (!user.HasPermission("Edit")) return;
if (order == null || order.Status != "Pending") return;

// actual logic (flat!)
```

---

### Pattern 3: Duplicate Validation

**Before:**
```csharp
// In 5 different places:
if (string.IsNullOrEmpty(email) || !email.Contains('@'))
    throw new ValidationException("Invalid email");
```

**After:**
```csharp
// Centralized:
EmailValidator.ValidateOrThrow(email);
```

---

## 📊 Metrics to Track

| Metric | Good | Medium | Bad |
|--------|------|--------|-----|
| Cyclomatic Complexity | < 10 | 10-15 | > 15 |
| Cognitive Complexity | < 10 | 10-20 | > 20 |
| Lines per Method | < 50 | 50-100 | > 100 |
| Method Parameters | < 4 | 4-6 | > 6 |
| Nesting Depth | < 3 | 3-4 | > 4 |
| Code Duplicates | 0 | 1-2 | > 2 |
| Maintainability Index | > 80 | 60-80 | < 60 |

---

**💡 Key Takeaway:** Use metrics to identify problems, semantic search to find duplicates, extract methods to simplify.

**Workflow:** AnalyzeComplexity → FindDuplicates → Refactor → Format → ApplyFixes → Verify

**Time saved:** 6-12x faster with measurable, quantifiable improvements.
