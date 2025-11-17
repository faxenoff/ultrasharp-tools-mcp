# Debugging Crash - Complete Guide

**Find crash root cause in 5-10 minutes instead of 1-2 hours.**

---

## 🎯 The Problem

**Typical scenario:** Production crash, need to find root cause.

**Manual approach (1-2 hours):**
1. Read stack trace, try to find relevant line
2. Open that file, read surrounding code
3. Guess what might have caused it
4. Trace backwards through code manually
5. Miss indirect call paths
6. Finally find root cause (maybe)

**MCP approach (5-10 minutes):**
1. `AnalyzeLogs` → extract stack trace
2. `TraceBackwards` → find ALL paths to crash
3. `TraceExecution` → understand flow
4. `ViewDefinition` → see implementation
5. `OverwriteMember` → fix bug

**Result:** 12-24x faster, find ALL possible causes (not just one).

---

## 🔧 Available Tools

### 1. AnalyzeLogs - Extract crash information

**5 supported formats with auto-detection:**
- ✅ ECS/JSON (Elastic Common Schema)
- ✅ Logcat (Android)
- ✅ WebServer (Apache/Nginx)
- ✅ XML (structured logs)
- ✅ PlainText (custom formats)

### 2. TraceBackwards - Reverse CFG analysis

**Finds ALL paths TO the crash point:**
- Works backwards from crash location
- Includes indirect paths
- Uses stack trace hints for precision
- SQLite caching (5-10x speedup on repeated queries)

### 3. TraceExecution - Forward CFG analysis

**Understands execution flow:**
- All possible paths through method
- Conditional branches
- Exception handling
- Loop behavior

### 4. AnalyzePathFeasibility - Z3 SMT solver

**Checks if path is actually reachable:**
- Symbolic execution
- Constraint solving
- Proves/disproves path feasibility

---

## 🚀 Step-by-Step Workflow

### Step 1: Analyze Log File

**Goal:** Extract stack trace and error details.

```csharp
UltrasharpTool_AnalyzeLogs(
    logFilePath: "logs/app-2025-01-17.log",
    logLevel: "Error",
    keywords: ["Exception", "Error", "Failed"]
)
```

**Response:**
```
📊 Log Analysis Results
Format detected: ECS/JSON
Total entries: 15,234
Error entries: 47
Warnings: 156

🔴 Top Errors:

1. NullReferenceException (12 occurrences)
   Last seen: 2025-01-17 14:23:45
   Location: UserService.UpdateProfile:67
   Message: "Object reference not set to an instance of an object"

   Stack trace:
   at MyApp.Services.UserService.UpdateProfile(User user)
   at MyApp.API.Controllers.UserController.Update(UpdateUserRequest request)
   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.Execute(...)

2. InvalidOperationException (8 occurrences)
   Last seen: 2025-01-17 14:15:32
   Location: OrderService.ProcessOrder:112
   ...

3. SqlException (5 occurrences)
   Last seen: 2025-01-17 13:45:23
   Location: OrderRepository.SaveAsync:45
   ...
```

**Time:** 3-5 seconds vs 10-20 minutes manually reading log.

**What you learn:**
- ✅ Most common error: NullReferenceException
- ✅ Location: UserService.UpdateProfile:67
- ✅ Entry point: UserController.Update
- ✅ Stack trace for backtrace

---

### Step 2: Trace Backwards from Crash

**Goal:** Find ALL code paths that lead to crash.

```csharp
UltrasharpTool_TraceBackwards(
    fullyQualifiedName: "MyApp.Services.UserService.UpdateProfile",
    maxDepth: 5,
    stackTraceHint: "UserController.Update → ValidationService.Validate → UserService.UpdateProfile"
)
```

**Response:**
```
🔍 Backwards Trace Results

Found 8 unique paths to UserService.UpdateProfile:

═══════════════════════════════════════
PATH 1 (Depth: 3) ⚠️ MATCHES STACK TRACE
═══════════════════════════════════════
UserController.Update
  → ValidationService.Validate
    → UserService.UpdateProfile  ← CRASH HERE

Confidence: HIGH (matches stack trace)
Estimated frequency: Common

═══════════════════════════════════════
PATH 2 (Depth: 4)
═══════════════════════════════════════
ScheduledJobsService.SyncUsersJob
  → UserSyncService.SyncFromExternalSystem
    → UserService.CreateOrUpdateUser
      → UserService.UpdateProfile  ← CRASH HERE

Confidence: MEDIUM
Estimated frequency: Hourly (scheduled job)

═══════════════════════════════════════
PATH 3 (Depth: 2)
═══════════════════════════════════════
AdminController.BulkUpdateUsers
  → UserService.UpdateProfile  ← CRASH HERE

Confidence: MEDIUM
Estimated frequency: Rare (admin action)

... (5 more paths)

💡 Recommendation: Focus on PATH 1 (matches stack trace) and PATH 2 (scheduled job - might explain intermittent failures)
```

**Time:** 10-15 seconds vs 30-60 minutes manually tracing.

**What you learn:**
- ✅ 8 different ways to reach crash point
- ✅ Most likely: PATH 1 (matches stack trace)
- ✅ Suspicious: PATH 2 (scheduled job, might not validate properly)
- ✅ Rare: PATH 3 (admin bulk update)

---

### Step 3: Understand Execution Flow

**Goal:** Understand what happens in UpdateProfile method.

```csharp
UltrasharpTool_TraceExecution(
    fullyQualifiedName: "MyApp.Services.UserService.UpdateProfile",
    maxDepth: 3
)
```

**Response:**
```
🔀 Execution Flow Analysis

Method: UserService.UpdateProfile(User user)

═══════════════════════════════════════
EXECUTION PATHS (4 found)
═══════════════════════════════════════

PATH A: Success path
─────────────────────────────────────
1. Entry: UpdateProfile(user)
2. Condition: if (user == null)  → FALSE
3. Call: _validator.ValidateUser(user)
4. Condition: if (!validationResult.IsValid)  → FALSE
5. Call: _repository.UpdateAsync(user)  ← MIGHT THROW
6. Call: _cache.InvalidateUser(user.Id)
7. Return: success

PATH B: Null user → ArgumentNullException
─────────────────────────────────────
1. Entry: UpdateProfile(user)
2. Condition: if (user == null)  → TRUE
3. Throw: new ArgumentNullException("user")

PATH C: Validation failed
─────────────────────────────────────
1. Entry: UpdateProfile(user)
2. Condition: if (user == null)  → FALSE
3. Call: _validator.ValidateUser(user)
4. Condition: if (!validationResult.IsValid)  → TRUE
5. Throw: new ValidationException(validationResult.Errors)

PATH D: Database exception
─────────────────────────────────────
1. Entry: UpdateProfile(user)
2. Condition: if (user == null)  → FALSE
3. Call: _validator.ValidateUser(user)
4. Condition: if (!validationResult.IsValid)  → FALSE
5. Call: _repository.UpdateAsync(user)
6. Exception: SqlException caught  ← MIGHT CAUSE NullRef later?
7. Call: _logger.LogError(ex)
8. Rethrow: wrapped in ServiceException

💡 Potential issue: Line 5 (_repository.UpdateAsync) might return null on failure instead of throwing
💡 Potential issue: Line 6 (_cache.InvalidateUser) might crash if user.Id is 0 or invalid
```

**Time:** 5-10 seconds vs 15-30 minutes manual code reading.

**What you learn:**
- ✅ 4 possible execution paths
- ✅ Potential issue: repository might return null
- ✅ Potential issue: cache invalidation might crash
- ✅ No null check after repository call

---

### Step 4: View Implementation Details

**Goal:** See actual code to confirm suspicions.

```csharp
UltrasharpTool_ViewDefinition(
    fullyQualifiedName: "MyApp.Services.UserService.UpdateProfile"
)
```

**Response:**
```csharp
public async Task<User> UpdateProfile(User user)
{
    // Check null
    if (user == null)
        throw new ArgumentNullException(nameof(user));

    // Validate
    var validationResult = await _validator.ValidateUser(user);
    if (!validationResult.IsValid)
        throw new ValidationException(validationResult.Errors);

    // Update in database
    var updatedUser = await _repository.UpdateAsync(user);  // ← Can return NULL!

    // Invalidate cache
    await _cache.InvalidateUser(updatedUser.Id);  // ← CRASH: NullReferenceException!
    // If updatedUser is null, accessing .Id throws!

    // Notify
    await _notificationService.NotifyProfileUpdated(updatedUser.Email);

    return updatedUser;
}
```

**BUG FOUND!**
- ❌ Line 15: `_repository.UpdateAsync()` can return `null` if update fails
- ❌ Line 18: Accessing `updatedUser.Id` without null check → NullReferenceException
- ❌ Line 21: Also accessing `updatedUser.Email` without null check

**Time:** Instant vs 5-10 minutes finding file and reading.

---

### Step 5: Investigate Repository Behavior

**Goal:** Why does repository return null?

```csharp
UltrasharpTool_ViewDefinition(
    fullyQualifiedName: "MyApp.Repositories.UserRepository.UpdateAsync"
)
```

**Response:**
```csharp
public async Task<User> UpdateAsync(User user)
{
    var existingUser = await _dbContext.Users.FindAsync(user.Id);

    if (existingUser == null)
    {
        _logger.LogWarning($"User {user.Id} not found for update");
        return null;  // ← PROBLEM: Returns null instead of throwing!
    }

    // Update properties
    existingUser.Name = user.Name;
    existingUser.Email = user.Email;
    existingUser.UpdatedAt = DateTime.UtcNow;

    await _dbContext.SaveChangesAsync();
    return existingUser;
}
```

**ROOT CAUSE IDENTIFIED:**
- Repository returns `null` when user not found
- Service doesn't check for `null` before using result
- Crash happens on next line: `updatedUser.Id`

---

### Step 6: Trace Scheduled Job Path

**Goal:** Why does scheduled job trigger this?

```csharp
UltrasharpTool_ViewDefinition(
    fullyQualifiedName: "MyApp.Services.UserSyncService.SyncFromExternalSystem"
)
```

**Response:**
```csharp
public async Task SyncFromExternalSystem()
{
    var externalUsers = await _externalApi.GetAllUsers();

    foreach (var externalUser in externalUsers)
    {
        var localUser = await _userRepository.FindByEmailAsync(externalUser.Email);

        if (localUser != null)
        {
            // Update existing user
            localUser.Name = externalUser.Name;
            localUser.Email = externalUser.Email;

            // ⚠️ ISSUE: Doesn't check if user still exists before update!
            await _userService.UpdateProfile(localUser);  // ← Can crash!
        }
        else
        {
            // Create new user
            await _userService.CreateUser(externalUser);
        }
    }
}
```

**RACE CONDITION FOUND:**
1. Job finds user by email
2. Between FindByEmailAsync and UpdateProfile, user might be deleted
3. UpdateProfile called with valid User object
4. Repository.UpdateAsync can't find user → returns null
5. Service crashes on `updatedUser.Id`

---

### Step 7: Fix the Bug

**Goal:** Add null checks and proper error handling.

```csharp
UltrasharpTool_OverwriteMember(
    fullyQualifiedTargetName: "MyApp.Services.UserService.UpdateProfile",
    codeSnippet: @"
public async Task<User> UpdateProfile(User user)
{
    // Check null
    if (user == null)
        throw new ArgumentNullException(nameof(user));

    // Validate
    var validationResult = await _validator.ValidateUser(user);
    if (!validationResult.IsValid)
        throw new ValidationException(validationResult.Errors);

    // Update in database
    var updatedUser = await _repository.UpdateAsync(user);

    // ✅ FIX: Check for null before using
    if (updatedUser == null)
    {
        _logger.LogError($""Failed to update user {user.Id}: User not found in database"");
        throw new InvalidOperationException($""User {user.Id} not found"");
    }

    // Invalidate cache (now safe)
    await _cache.InvalidateUser(updatedUser.Id);

    // Notify (now safe)
    await _notificationService.NotifyProfileUpdated(updatedUser.Email);

    return updatedUser;
}
",
    commitMessage: "Fix NullReferenceException in UpdateProfile - add null check after repository update"
)
```

**Response:**
```
✅ Member overwritten successfully

📊 Code Quality Check:
✅ No compilation errors
✅ No warnings
ℹ️ Consider running FormatCode for consistent style

🌳 Git:
Branch: ultrasharptools/20250117-142530
Commit: a7f3e91 "Fix NullReferenceException in UpdateProfile - add null check after repository update"
```

---

## 📋 Complete Workflow Template

**Use this for every crash investigation:**

```
1. AnalyzeLogs(logFilePath: "...", logLevel: "Error", keywords: ["Exception"])
   → Extract stack trace and error details

2. TraceBackwards(
     fullyQualifiedName: "CrashMethod",
     stackTraceHint: "path from stack trace"
   )
   → Find ALL paths to crash (not just one!)

3. TraceExecution(fullyQualifiedName: "CrashMethod", maxDepth: 3)
   → Understand execution flow, identify potential issues

4. ViewDefinition(fullyQualifiedName: "CrashMethod")
   → See actual code, confirm suspicions

5. ViewDefinition(fullyQualifiedName: "SuspiciousDependency")
   → Investigate dependencies mentioned in crash

6. AnalyzePathFeasibility(path: [...])  // Optional
   → Verify if suspected path is actually reachable

7. OverwriteMember(...)
   → Fix the bug

8. FormatCode + AnalyzeCodeStyle + ApplyCodeFixes
   → Ensure quality
```

**Total time:** 5-15 minutes vs 1-3 hours manually.

---

## 🎯 Real-World Example: Production Database Crash

**Scenario:** Production application crashes with SqlException, need to find why.

### Minutes 0-2: Extract Error Info

```csharp
AnalyzeLogs(
    logFilePath: "/var/log/app/production.log",
    logLevel: "Error",
    keywords: ["SqlException", "Timeout", "Deadlock"]
)
```

**Results:**
```
🔴 SqlException (47 occurrences in last hour!)

Pattern detected: "Timeout expired" (32 times)
Pattern detected: "Deadlock detected" (15 times)

Stack trace (most common):
at OrderRepository.SaveAsync:78
at OrderService.CreateOrder:145
at OrderController.PlaceOrder:45
```

**Problem:** Database timeouts and deadlocks.

---

### Minutes 2-4: Find All Paths

```csharp
TraceBackwards(
    fullyQualifiedName: "OrderRepository.SaveAsync",
    maxDepth: 6,
    stackTraceHint: "OrderController.PlaceOrder → OrderService.CreateOrder → OrderRepository.SaveAsync"
)
```

**Results:**
```
Found 15 paths to OrderRepository.SaveAsync

PATH 1: Normal order creation (matches stack trace)
PATH 2: Bulk order import (admin tool)
PATH 3: Scheduled order sync job
PATH 4: Webhook from payment processor
PATH 5: Cart auto-checkout (abandoned cart recovery)
...

💡 All paths call SaveAsync WITHOUT checking for existing transactions
💡 Potential deadlock: Multiple paths can run concurrently
```

---

### Minutes 4-6: Analyze Execution

```csharp
TraceExecution(
    fullyQualifiedName: "OrderRepository.SaveAsync",
    maxDepth: 4
)
```

**Results:**
```
Execution analysis reveals:

1. Opens new transaction ← PROBLEM!
2. Locks Order table
3. Updates Order
4. Locks OrderItems table
5. Inserts OrderItems
6. Locks Inventory table
7. Updates Inventory
8. Commits transaction

⚠️ ISSUE: Inconsistent lock order!
- Some callers lock Inventory first
- This method locks Order first
→ Classic deadlock scenario!

⚠️ ISSUE: No transaction timeout set
→ Explains infinite hangs
```

---

### Minutes 6-8: Find Deadlock Partner

```csharp
// Find what else locks Inventory
FindPotentialDuplicates(
    targetCode: "await _dbContext.Database.BeginTransactionAsync(); var inventory = await _dbContext.Inventory.FindAsync();",
    threshold: 0.6
)
```

**Results:**
```
1. InventoryService.UpdateStock (similarity: 0.78)
   Locks: Inventory → Order → OrderItems
   ← OPPOSITE LOCK ORDER!

2. InventorySync.SyncFromWarehouse (similarity: 0.72)
   Locks: Inventory → Product

3. RestockService.AutoRestock (similarity: 0.68)
   Locks: Inventory → Order
```

**DEADLOCK FOUND:**
- OrderRepository.SaveAsync locks: Order → Inventory
- InventoryService.UpdateStock locks: Inventory → Order
- When they run concurrently → DEADLOCK!

---

### Minutes 8-10: Fix with Consistent Lock Order

```csharp
// Refactor to always lock in same order: Inventory → Order → OrderItems
OverwriteMember(
    fullyQualifiedTargetName: "OrderRepository.SaveAsync",
    codeSnippet: "/* new implementation with consistent lock order */",
    commitMessage: "Fix deadlock by enforcing consistent lock order (Inventory → Order → OrderItems)"
)
```

---

**Total time:** 10 minutes
**Root cause found:** Inconsistent lock order causing deadlocks
**Manual debugging:** Would take 2-4 hours + might miss concurrent paths

---

## 💡 Best Practices

### 1. Always Use Stack Trace Hints

```csharp
✅ GOOD:
TraceBackwards(
    fullyQualifiedName: "UserService.UpdateProfile",
    stackTraceHint: "UserController.Update → ValidationService → UserService.UpdateProfile"
)
→ Prioritizes matching path, finds it faster

❌ BAD:
TraceBackwards(
    fullyQualifiedName: "UserService.UpdateProfile"
)
→ Finds all paths equally, harder to identify relevant one
```

### 2. Check Concurrent Paths

```csharp
// Crash in scheduled job? Check all paths!
TraceBackwards(fullyQualifiedName: "CrashMethod", maxDepth: 6)

// Look for:
- Scheduled jobs
- Background workers
- Event handlers
- Webhooks
- Admin tools

→ Race conditions hide in concurrent execution!
```

### 3. Verify Assumptions with Path Feasibility

```csharp
// Think path is impossible? Verify!
AnalyzePathFeasibility(
    path: ["MethodA", "MethodB", "MethodC"],
    constraints: ["user != null", "user.IsActive == true"]
)

→ Might find path IS reachable under certain conditions
```

### 4. Investigate Dependencies

```csharp
// Don't just look at crash method!
ViewDefinition("CrashMethod")
→ See calls to: _repository, _cache, _validator

ViewDefinition("Repository.MethodCalled")
→ Might find: returns null instead of throwing

ViewDefinition("Cache.MethodCalled")
→ Might find: doesn't handle missing keys
```

---

## ⚠️ Common Patterns & Solutions

### Pattern 1: Null Reference After Repository Call

**Symptom:**
```
NullReferenceException at line after repository call
```

**Root cause:**
```csharp
var entity = await _repository.FindAsync(id);
entity.Property = value;  // ← CRASH if FindAsync returns null
```

**Solution:**
```csharp
var entity = await _repository.FindAsync(id);
if (entity == null)
    throw new NotFoundException($"Entity {id} not found");

entity.Property = value;  // Safe
```

---

### Pattern 2: Race Condition in Async Code

**Symptom:**
```
Intermittent crashes, hard to reproduce
Often in scheduled jobs or background workers
```

**Root cause:**
```csharp
var user = await _repository.FindAsync(id);  // User exists
// ← Another thread deletes user here!
await _repository.UpdateAsync(user);  // ← CRASH: user deleted
```

**Solution:**
```csharp
using var transaction = await _db.BeginTransactionAsync();
var user = await _repository.FindAsync(id);
if (user == null)
    throw new NotFoundException();

await _repository.UpdateAsync(user);  // Protected by transaction
await transaction.CommitAsync();
```

---

### Pattern 3: Missing Null Checks in Chain

**Symptom:**
```
NullReferenceException in property access
user.Address.Street → crash
```

**Root cause:**
```csharp
var street = user.Address.Street;  // ← CRASH if Address is null
```

**Solution:**
```csharp
var street = user?.Address?.Street ?? "N/A";  // Null-conditional operator
```

---

### Pattern 4: Exception Swallowing

**Symptom:**
```
Silent failures, data inconsistency
Errors don't appear in logs
```

**Root cause:**
```csharp
try {
    await SaveToDatabase();
} catch {
    // ← Swallows exception, no logging!
}
```

**Solution:**
```csharp
try {
    await SaveToDatabase();
} catch (Exception ex) {
    _logger.LogError(ex, "Failed to save to database");
    throw;  // Re-throw or handle properly
}
```

---

## 📊 Tool Comparison

| Tool | Purpose | When to Use |
|------|---------|-------------|
| **AnalyzeLogs** | Extract errors from logs | Start of investigation |
| **TraceBackwards** | Find paths TO crash | Know crash location |
| **TraceExecution** | Understand method flow | Understand what method does |
| **AnalyzePathFeasibility** | Verify path reachability | Doubt if path is possible |
| **ViewDefinition** | See implementation | Confirm suspicions |
| **FindPotentialDuplicates** | Find similar crashes | Look for patterns |

---

**💡 Key Takeaway:** Use TraceBackwards to find ALL paths to crash, not just the obvious one.

**Workflow:** AnalyzeLogs → TraceBackwards → TraceExecution → ViewDefinition → Fix

**Time saved:** 12-36x faster than manual debugging.
