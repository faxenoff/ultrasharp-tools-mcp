# Semantic Search - Complete Guide

**Find code by MEANING, not by name. No FQN required.**

---

## 🎯 Problem Statement

**Traditional search limitations:**
- ❌ Need exact class/method name
- ❌ Can't find code with different variable names
- ❌ Miss duplicates with similar logic but different syntax
- ❌ Grep/regex only finds textual matches

**Semantic search solves this:**
- ✅ Finds code by **meaning**, not text
- ✅ Works **without** knowing exact names
- ✅ Finds duplicates even if syntax differs
- ✅ Understands **context** and **intent**

---

## 🔧 Tool: find_duplicates

```csharp
find_duplicates(
    targetCode: string,      // Example of what you're looking for
    threshold: double,       // Similarity threshold (0.0-1.0)
    skip: int = 0,          // Pagination: skip N results
    take: int = 20          // Pagination: take N results
)
```

### Parameters:

**targetCode** - Example of code you're searching for
- Can be: method signature, code snippet, pattern
- Doesn't need to be exact - semantic matching!
- Examples below

**threshold** - How similar results should be (0.0 to 1.0)
- `0.5-0.6` - Very broad search (finds loosely related code)
- `0.7-0.75` - Balanced (finds similar functionality)
- `0.8-0.9` - Strict (finds near-duplicates)
- `0.95+` - Exact duplicates only

**skip/take** - Pagination for large result sets
- Default: 0/20 (first 20 results)
- Use for browsing many matches

---

## 📊 Example Scenarios

### Scenario 1: Find HTTP Request Handlers

**Task:** "Where in the codebase are HTTP requests handled?"

**Traditional approach (grep):**
```bash
grep -r "HttpContext" .
grep -r "IActionResult" .
grep -r "Controller" .
# → 500+ matches, mostly irrelevant
# → Need to manually filter noise
# → Miss handlers with different naming
```

**Semantic search:**
```csharp
FindPotefind_duplicatesrgetCode: "async Task<IActionResult> HandleRequest(HttpContext context)",
    threshold: 0.7
)
```

**Results:**
```
✅ UserController.CreateUser(HttpContext ctx)
✅ OrderController.ProcessOrder(HttpContext httpContext)
✅ PaymentService.HandlePaymentRequest(HttpContext request)
✅ AuthMiddleware.InvokeAsync(HttpContext context)
```

**Why it works:**
- Finds all HTTP handlers regardless of names
- Understands semantic pattern: "async + IActionResult + HttpContext"
- Ignores variable naming differences

---

### Scenario 2: Find Error Logging Patterns

**Task:** "Where are errors logged and saved to database?"

**Traditional approach:**
```bash
grep -r "LogError" .
grep -r "SaveAsync" .
# → Hundreds of separate matches
# → Can't find PATTERN of "log + save"
# → Miss different logger names (ILogger, Logger, _log, etc.)
```

**Semantic search:**
```csharp
FindPotentialDuplicates(
    targetCode: "logger.LogError(exception, message); await database.SaveAsync();",
    threshold: 0.6
)
```

**Results:**
```
✅ UserService.cs:45
    _logger.LogError(ex, "User creation failed");
    await _dbContext.SaveChangesAsync();

✅ OrderService.cs:112
    Logger.Error(exception, "Order processing error");
    await _repository.CommitAsync();

✅ PaymentProcessor.cs:78
    log.LogCritical(error, "Payment failed");
    await unitOfWork.SaveAsync();
```

**Why it works:**
- Finds PATTERN: "error logging + database save"
- Works with different logger types (ILogger, Logger, log)
- Works with different DB save methods (SaveAsync, Commit, SaveChanges)

---

### Scenario 3: Find Duplicate Validation Logic

**Task:** "Are there duplicate email validation methods?"

**Traditional approach:**
```bash
grep -r "email" . | grep -i "valid"
# → 1000+ matches (variable names, comments, strings)
# → Can't distinguish validation logic from other uses
```

**Semantic search:**
```csharp
FindPotentialDuplicates(
    targetCode: "bool IsValidEmail(string email) { return email.Contains('@') && email.Contains('.'); }",
    threshold: 0.8
)
```

**Results:**
```
✅ UserValidator.cs:23 (similarity: 0.95)
    public bool ValidateEmail(string emailAddress) {
        return emailAddress.Contains("@") && emailAddress.Contains(".");
    }

✅ RegistrationService.cs:56 (similarity: 0.87)
    private bool CheckEmailFormat(string mail) {
        return mail.IndexOf('@') > 0 && mail.IndexOf('.') > mail.IndexOf('@');
    }

✅ EmailHelper.cs:12 (similarity: 0.82)
    public static bool IsEmailValid(string input) {
        var hasAt = input.Contains('@');
        var hasDot = input.Contains('.');
        return hasAt && hasDot;
    }
```

**Why it works:**
- Finds similar LOGIC, not just text
- Works despite different:
  - Method names (IsValidEmail, ValidateEmail, CheckEmailFormat)
  - Parameter names (email, emailAddress, mail, input)
  - Implementation details (Contains vs IndexOf)

---

### Scenario 4: Find Authentication Checks

**Task:** "Where do we check if user is authenticated?"

**Semantic search:**
```csharp
FindPotentialDuplicates(
    targetCode: "if (user == null || !user.IsAuthenticated) { return Unauthorized(); }",
    threshold: 0.7
)
```

**Results:**
```
✅ AuthMiddleware.cs:34
    if (currentUser == null || !currentUser.IsLoggedIn) {
        return new UnauthorizedResult();
    }

✅ BaseController.cs:67
    if (User?.Identity?.IsAuthenticated != true) {
        return Unauthorized();
    }

✅ SecureEndpoint.cs:12
    if (!HttpContext.User.Identity.IsAuthenticated) {
        throw new UnauthorizedException();
    }
```

**Why it works:**
- Finds pattern: "null check + authenticated check → unauthorized"
- Different implementations (middleware, controller, attribute)
- Different property names (IsAuthenticated, IsLoggedIn, Identity.IsAuthenticated)

---

### Scenario 5: Find Database Query Patterns

**Task:** "Where do we query users by email?"

**Semantic search:**
```csharp
FindPotentialDuplicates(
    targetCode: "var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);",
    threshold: 0.75
)
```

**Results:**
```
✅ UserRepository.cs:45
    var existingUser = await _context.Users
        .Where(x => x.EmailAddress == emailAddress)
        .FirstOrDefaultAsync();

✅ AuthService.cs:89
    var user = await Database.Users.SingleOrDefaultAsync(u => u.Email == providedEmail);

✅ UserQuery.cs:23
    return await _db.Users.FirstAsync(user => user.Email.Equals(email));
```

**Why it works:**
- Finds pattern: "query Users + filter by Email + async"
- Different LINQ methods (FirstOrDefault, Where + First, Single)
- Different variable names

---

## 🎚️ Threshold Guide

### When to use different thresholds:

**0.5 - 0.6: Very Broad Search**
- Exploring unfamiliar codebase
- "Show me anything related to..."
- Finding loosely related functionality
- Risk: Many false positives

**0.7 - 0.75: Balanced Search (RECOMMENDED)**
- Finding similar functionality
- "Where is X implemented?"
- Discovering patterns across codebase
- Good balance: precision vs recall

**0.8 - 0.9: Strict Search**
- Finding near-duplicates
- Code review for duplicates
- Refactoring candidates
- Higher precision, might miss some matches

**0.95+: Exact Duplicates Only**
- Copy-paste detection
- Exact code duplication
- Very high precision
- Might miss slight variations

---

## 💡 Best Practices

### 1. Start Broad, Then Narrow

```csharp
// Step 1: Broad search to explore
FindPotentialDuplicates(
    targetCode: "process payment",
    threshold: 0.6
)
// → Review results, understand patterns

// Step 2: Narrow down
FindPotentialDuplicates(
    targetCode: "async Task<PaymentResult> ProcessPayment(decimal amount, string cardNumber)",
    threshold: 0.8
)
// → More specific results
```

### 2. Use Representative Examples

```csharp
❌ BAD: targetCode: "DoSomething()"
   // Too generic, not enough context

✅ GOOD: targetCode: "async Task<bool> ValidateUserCredentials(string username, string password)"
   // Clear intent, enough context
```

### 3. Include Context in Search

```csharp
❌ BAD: targetCode: "x + y"
   // No context

✅ GOOD: targetCode: "decimal total = price * quantity + tax;"
   // Context: calculating totals with tax
```

### 4. Combine with Other Tools

```csharp
// Step 1: Semantic search to find relevant code
var results = FindPotentialDuplicates(
    targetCode: "save user to database",
    threshold: 0.7
);

// Step 2: View specific implementations
ViewDefinition(fullyQualifiedName: "UserRepository.SaveAsync");

// Step 3: Find all usages
find_references(fullyQualifiedName: "UserRepository.SaveAsync");

// Step 4: Analyze complexity
analyze_complexity(fullyQualifiedName: "UserRepository.SaveAsync");
```

---

## 🚀 Advanced Techniques

### Pattern 1: Multi-Step Search

**Find authentication → find authorization → find role checks**

```csharp
// Step 1: Find authentication
FindPotentialDuplicates(
    targetCode: "if (!user.IsAuthenticated) return Unauthorized();",
    threshold: 0.7
)

// Step 2: From results, find authorization
FindPotentialDuplicates(
    targetCode: "if (!user.HasRole('Admin')) return Forbidden();",
    threshold: 0.7
)

// Step 3: Find specific role checks
FindPotentialDuplicates(
    targetCode: "user.Roles.Contains('SuperAdmin')",
    threshold: 0.8
)
```

### Pattern 2: Architecture Discovery

**Find all repository patterns in codebase**

```csharp
FindPotentialDuplicates(
    targetCode: "public async Task<T> GetByIdAsync<T>(int id) { return await _context.Set<T>().FindAsync(id); }",
    threshold: 0.7
)
// → Discovers all repository implementations
// → Understand repository architecture
```

### Pattern 3: Error Handling Discovery

**Find all error handling patterns**

```csharp
// Find try-catch with logging
FindPotentialDuplicates(
    targetCode: "try { } catch (Exception ex) { logger.LogError(ex); throw; }",
    threshold: 0.6
)

// Find error wrapping
FindPotentialDuplicates(
    targetCode: "throw new ApplicationException('Error processing request', ex);",
    threshold: 0.7
)
```

---

## ⚠️ Common Pitfalls

### Pitfall 1: Too Generic Search

```csharp
❌ WRONG:
FindPotentialDuplicates(
    targetCode: "var x = y;",
    threshold: 0.5
)
// → Thousands of meaningless matches

✅ RIGHT:
FindPotentialDuplicates(
    targetCode: "var validatedUser = await userValidator.ValidateAsync(userDto);",
    threshold: 0.7
)
// → Specific validation patterns
```

### Pitfall 2: Threshold Too High

```csharp
❌ WRONG:
FindPotentialDuplicates(
    targetCode: "process payment",
    threshold: 0.95
)
// → Misses everything (too strict for short query)

✅ RIGHT:
FindPotentialDuplicates(
    targetCode: "async Task<PaymentResult> ProcessPayment(PaymentRequest request)",
    threshold: 0.95
)
// → High threshold works with detailed query
```

### Pitfall 3: Ignoring Results

```csharp
❌ WRONG:
FindPotentialDuplicates(...)
// Look at first result only
// → Miss important duplicates

✅ RIGHT:
FindPotentialDuplicates(...)
// Review all results with similarity > 0.75
// Use skip/take to paginate if many results
```

---

## 📊 Performance Tips

### Tip 1: Use Pagination for Large Results

```csharp
// First page
FindPotentialDuplicates(
    targetCode: "...",
    threshold: 0.7,
    skip: 0,
    take: 20
)

// Second page (if needed)
FindPotentialDuplicates(
    targetCode: "...",
    threshold: 0.7,
    skip: 20,
    take: 20
)
```

### Tip 2: Adjust Threshold Based on Results

```csharp
// If too many results → increase threshold
FindPotentialDuplicates(targetCode: "...", threshold: 0.7)
// → 100 results (too many)

FindPotentialDuplicates(targetCode: "...", threshold: 0.85)
// → 15 results (better)

// If too few results → decrease threshold
FindPotentialDuplicates(targetCode: "...", threshold: 0.9)
// → 0 results

FindPotentialDuplicates(targetCode: "...", threshold: 0.7)
// → 8 results (good)
```

---

## 🎓 Real-World Example: Complete Workflow

**Scenario:** New to codebase, need to add new payment method

### Step 1: Discover existing payment processing

```csharp
FindPotentialDuplicates(
    targetCode: "async Task<PaymentResult> ProcessPayment(decimal amount)",
    threshold: 0.7
)
```

**Results:**
```
✅ CreditCardPayment.ProcessCreditCard (similarity: 0.82)
✅ PayPalPayment.ProcessPayPal (similarity: 0.78)
✅ BankTransfer.ProcessTransfer (similarity: 0.75)
```

### Step 2: View specific implementation

```csharp
ViewDefinition(fullyQualifiedName: "CreditCardPayment.ProcessCreditCard")
```

**Learn:**
- How validation works
- How transactions are logged
- How errors are handled
- Return value structure

### Step 3: Find where payment methods are registered

```csharp
FindPotentialDuplicates(
    targetCode: "services.AddScoped<IPaymentProcessor, CreditCardPayment>();",
    threshold: 0.8
)
```

**Results:**
```
✅ Startup.cs:67 - Payment registration
```

### Step 4: Find where payment methods are called

```csharp
FindReferences(fullyQualifiedName: "IPaymentProcessor.ProcessPayment")
```

### Step 5: Implement new payment method

Now you know:
- ✅ Interface to implement
- ✅ Registration pattern
- ✅ Error handling pattern
- ✅ Validation approach
- ✅ Logging conventions

**Total time:** 5-10 minutes vs 1-2 hours manually exploring code.

---

**💡 Key Takeaway:** Semantic search is your **FIRST TOOL** when you don't know exact class names.

**Use it immediately → Save hours of manual code reading.**
