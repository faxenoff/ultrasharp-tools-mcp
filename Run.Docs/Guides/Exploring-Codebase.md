# Exploring Unfamiliar Codebase - Complete Guide

**Learn 50+ file project in 2-3 minutes instead of 1-2 hours.**

---

## 🎯 The Problem

**Typical scenario:** You're assigned to work on unfamiliar C# project.

**Manual approach (1-2 hours):**
1. Open Solution Explorer → expand folders
2. Read each file name, try to guess purpose
3. Open random files, read code
4. Try to build mental map of structure
5. Get lost, forget what you saw
6. Repeat until you understand

**MCP approach (2-3 minutes):**
1. `load_solution` → initialize
2. `load_project` → get full structure map
3. `find_duplicates` → find what you need
4. `view_definition` → understand implementation
5. `find_references` → see usage patterns

**Result:** 30-60x faster, complete understanding.

---

## 🚀 Step-by-Step Workflow

### Step 1: Initialize Workspace

**ALWAYS start with this!**

```csharp
load_solution("D:/Projects/MyApp/MyApp.sln")
```

**What happens:**
- ✅ MSBuild workspace initialized
- ✅ All projects loaded
- ✅ Dependencies resolved
- ✅ Symbol index built (21-355 seconds for 355K symbols)

**Response example:**
```
✅ Solution loaded successfully
Projects: 8
Total files: 234
Total symbols: 12,450
Index time: 8.3 seconds
```

**Without this step:** All other tools will FAIL.

---

### Step 2: Get High-Level Overview

**Goal:** Understand project structure at 10,000 foot level.

```csharp
load_project(
    projectName: "MyApp.Core",
    detailLevel: "TypesOnly"
)
```

**Response structure:**
```
MyApp.Core
├── MyApp.Core.Domain
│   ├── User
│   ├── Order
│   ├── Product
│   └── Payment
│
├── MyApp.Core.Services
│   ├── UserService
│   ├── OrderService
│   ├── PaymentProcessor
│   └── EmailSender
│
├── MyApp.Core.Repositories
│   ├── UserRepository
│   ├── OrderRepository
│   └── ProductRepository
│
└── MyApp.Core.Utilities
    ├── DateHelper
    ├── StringExtensions
    └── Validator
```

**Time:** 2-5 seconds vs 15-30 minutes manually.

**What you learn:**
- ✅ Project namespaces (Domain, Services, Repositories)
- ✅ Key types in each namespace
- ✅ Architecture patterns (DDD, Repository, Services)
- ✅ Project organization

---

### Step 3: Get Detailed View of Key Areas

**Goal:** Understand specific namespace in detail.

```csharp
load_project(
    projectName: "MyApp.Core",
    detailLevel: "TypesAndSignatures"
)
```

**Response includes method signatures:**
```
MyApp.Core.Services.UserService
├── Task<User> GetUserByIdAsync(int id)
├── Task<User> GetUserByEmailAsync(string email)
├── Task<bool> ValidateUserAsync(User user)
├── Task CreateUserAsync(User user)
├── Task UpdateUserAsync(User user)
└── Task DeleteUserAsync(int id)

MyApp.Core.Services.OrderService
├── Task<Order> CreateOrderAsync(CreateOrderRequest request)
├── Task<Order> GetOrderAsync(int orderId)
├── Task<List<Order>> GetUserOrdersAsync(int userId)
├── Task CancelOrderAsync(int orderId)
└── Task<decimal> CalculateTotalAsync(int orderId)
```

**Time:** 5-10 seconds vs 30-60 minutes manually.

**What you learn:**
- ✅ Available methods in each service
- ✅ Method signatures (parameters, return types)
- ✅ Async patterns usage
- ✅ Naming conventions

---

### Step 4: Find Specific Functionality

**Goal:** "Where is payment processing implemented?"

**Problem:** You don't know exact class name.

**Solution:** Semantic search!

```csharp
find_duplicates(
    targetCode: "async Task<PaymentResult> ProcessPayment(decimal amount, string cardNumber)",
    threshold: 0.7
)
```

**Results:**
```
1. PaymentProcessor.ProcessCreditCardAsync (similarity: 0.85)
   Location: MyApp.Core.Services.PaymentProcessor:45

2. StripePaymentService.ChargeCardAsync (similarity: 0.78)
   Location: MyApp.Infrastructure.Payments.StripePaymentService:67

3. PayPalService.ProcessPaymentAsync (similarity: 0.72)
   Location: MyApp.Infrastructure.Payments.PayPalService:34
```

**Time:** 5-10 seconds vs 20-40 minutes manually.

**Now you know:**
- ✅ Multiple payment implementations exist
- ✅ Main one: PaymentProcessor
- ✅ External integrations: Stripe, PayPal
- ✅ Exact locations

---

### Step 5: Understand Implementation

**Goal:** See how payment processing actually works.

```csharp
view_definition(
    fullyQualifiedName: "MyApp.Core.Services.PaymentProcessor.ProcessCreditCardAsync"
)
```

**Response includes:**
```csharp
/// <summary>
/// Processes credit card payment
/// </summary>
public async Task<PaymentResult> ProcessCreditCardAsync(
    decimal amount,
    string cardNumber,
    string cvv,
    DateTime expiryDate)
{
    // Validate card
    if (!_cardValidator.ValidateCard(cardNumber, cvv, expiryDate))
    {
        return PaymentResult.Failed("Invalid card");
    }

    // Process through gateway
    var gatewayResult = await _paymentGateway.ChargeAsync(amount, cardNumber);

    // Log transaction
    await _transactionLogger.LogAsync(gatewayResult);

    // Save to database
    await _paymentRepository.SaveAsync(gatewayResult);

    return gatewayResult.IsSuccess
        ? PaymentResult.Success(gatewayResult.TransactionId)
        : PaymentResult.Failed(gatewayResult.ErrorMessage);
}
```

**Additional context provided:**
- ✅ XML documentation
- ✅ Dependencies: `_cardValidator`, `_paymentGateway`, `_transactionLogger`
- ✅ Calls to: ValidateCard, ChargeAsync, LogAsync, SaveAsync
- ✅ Return type: PaymentResult

**Time:** Instant vs 5-10 minutes finding and reading file.

**What you learn:**
- ✅ Validation approach
- ✅ External gateway integration
- ✅ Logging pattern
- ✅ Error handling pattern
- ✅ Database persistence

---

### Step 6: Find Usage Patterns

**Goal:** "Where is ProcessCreditCardAsync called?"

```csharp
find_references(
    fullyQualifiedName: "MyApp.Core.Services.PaymentProcessor.ProcessCreditCardAsync",
    includeSnippets: true
)
```

**Results:**
```
Found 8 references:

1. MyApp.API.Controllers.PaymentController.ProcessPayment:45
   var result = await _paymentProcessor.ProcessCreditCardAsync(
       request.Amount,
       request.CardNumber,
       request.Cvv,
       request.ExpiryDate
   );

2. MyApp.Core.Services.OrderService.CompleteOrder:112
   var paymentResult = await _paymentProcessor.ProcessCreditCardAsync(
       order.Total,
       order.PaymentDetails.CardNumber,
       order.PaymentDetails.Cvv,
       order.PaymentDetails.ExpiryDate
   );

3. MyApp.Core.Services.SubscriptionService.ChargeSubscription:78
   var result = await _paymentProcessor.ProcessCreditCardAsync(
       subscription.MonthlyFee,
       subscription.Card.Number,
       subscription.Card.Cvv,
       subscription.Card.Expiry
   );

... (5 more)
```

**Time:** 3-5 seconds vs 10-20 minutes with grep.

**What you learn:**
- ✅ Called from: API controller, OrderService, SubscriptionService
- ✅ Different contexts: one-time payment, order completion, subscription
- ✅ How parameters are passed
- ✅ Common patterns

---

### Step 7: Understand Dependencies

**Goal:** "What interfaces/dependencies does this use?"

From ViewDefinition response, you saw:
- `_cardValidator`
- `_paymentGateway`
- `_transactionLogger`
- `_paymentRepository`

**Explore each:**

```csharp
// Find validator implementation
find_duplicates(
    targetCode: "bool ValidateCard(string cardNumber, string cvv, DateTime expiry)",
    threshold: 0.8
)

// Find gateway implementations
list_implementations(
    fullyQualifiedName: "MyApp.Core.Interfaces.IPaymentGateway"
)
```

**Results:**
```
IPaymentGateway implementations:
1. StripeGateway
2. PayPalGateway
3. SquareGateway
4. MockGateway (for testing)
```

**Now you understand:**
- ✅ Abstraction pattern (interface-based)
- ✅ Multiple gateway support
- ✅ Testability (MockGateway)

---

## 📋 Complete Workflow Template

**Use this every time you explore unfamiliar project:**

```
1. load_solution("path/to/solution.sln")
   → Initialize workspace

2. LoadProject(projectName: "ProjectName", detailLevel: "TypesOnly")
   → Get high-level overview

3. load_project(projectName: "ProjectName", detailLevel: "TypesAndSignatures")
   → Get detailed view of key namespaces

4. find_duplicates(targetCode: "what I'm looking for", threshold: 0.7)
   → Find specific functionality (semantic search!)

5. ViewDefinition(fullyQualifiedName: "FullClassName.MethodName")
   → Understand implementation

6. find_references(fullyQualifiedName: "FullClassName.MethodName")
   → See usage patterns

7. list_implementations(fullyQualifiedName: "IInterfaceName")
   → Understand abstraction layers

8. AnalyzeComplexity(fullyQualifiedName: "ClassName", includeMembers: true)
   → Identify complex/problematic code
```

**Total time:** 2-5 minutes for complete understanding.

---

## 🎯 Real-World Example: E-Commerce Project

**Scenario:** New developer joins e-commerce project. Needs to understand order processing.

### Minutes 0-1: Initialize & Overview

```csharpload_solutionn("D:/Projects/ECommerce/ECommerce.sln")

LoadProject(projectName: "ECommerce.Core", detailLevel: "TypesOnly")
```

**Learn:**
```
ECommerce.Core
├── Domain (User, Order, Product, Payment, Shipping)
├── Services (OrderService, PaymentService, ShippingService)
├── Repositories (OrderRepository, ProductRepository)
└── Validators (OrderValidator, PaymentValidator)
```

**Understanding:** Classic DDD architecture with services and repositories.

---

### Minutes 1-2: Find Order Processing

```csharp
FindPotefind_duplicatesrgetCode: "async Task<Order> ProcessOrder(CreateOrderRequest request)",
    threshold: 0.7
)
```

**Results:**
```
1. OrderService.CreateOrderAsync (similarity: 0.89)
2. OrderProcessor.ProcessNewOrder (similarity: 0.82)
3. CheckoutService.CompleteCheckout (similarity: 0.75)
```

**Decision:** OrderService.CreateOrderAsync is main entry point.

---

### Minutes 2-3: Understand Flow

```csharp
ViewDefinition(
    fullyQualifiedName: "ECommerce.Core.Services.OrderService.CreateOrderAsync"
)
```

**Learn implementation:**
1. Validates order → `_orderValidator.ValidateAsync()`
2. Checks inventory → `_inventoryService.CheckAvailability()`
3. Calculates total → `_pricingService.CalculateTotal()`
4. Processes payment → `_paymentService.ProcessPayment()`
5. Creates shipping → `_shippingService.CreateShipment()`
6. Saves order → `_orderRepository.SaveAsync()`
7. Sends confirmation → `_emailService.SendOrderConfirmation()`

**Complete understanding of order flow in 30 seconds!**

---

### Minutes 3-4: Explore Dependencies

```csharp
// Understand payment processing
view_definition(
    fullyQualifiedName: "ECommerce.Core.Services.PaymentService.ProcessPayment"
)

// See how inventory worksview_definitionn(
    fullyQualifiedName: "ECommerce.Core.Services.InventoryService.CheckAvailability"
)

// Understand shipping
ViewDefinition(
    fullyQualifiedName: "ECommerce.Core.Services.ShippingService.CreateShipment"
)
```

---

### Minutes 4-5: Find Edge Cases

```csharp
// Find error handling
FindPotentialDuplicates(
    targetCode: "if (order == null) throw new OrderNotFoundException();",
    threshold: 0.7
)

// Find retry logic
FindPotentialDuplicates(
    targetCode: "for (int i = 0; i < maxRetries; i++) { try { await payment.Charge(); break; } catch { } }",
    threshold: 0.6
)
```

---

**Total time:** 5 minutes
**Understanding level:** 80-90% (vs 20-30% after 2 hours manual exploration)

---

## 💡 Best Practices

### 1. Start Broad, Drill Down

```csharp
✅ CORRECT ORDER:
1.load_projectt(detailLevel: "TypesOnly")        // Overview
2load_projectct(detailLevel: "TypesAndSignatures") // Details
3. FindPotentialDupfind_duplicates      // Find specific
4view_definitionon(...)                          // Understand

❌ WRONG ORDER:
view_definitionion(random class)  // Don't know what to look at
2. Read random files             // No context
```

### 2. Use Semantic Search Aggressively

```csharp
❌ WRONG: "I don't know exact class name, so I'll browse files"

✅ RIGHT: FindPotentialDuplicates(targetCode: "example of what I need")
   → Get exact FQN
   → Then ViewDefinition
```

### 3. Follow the Call Chain

```csharp
// Entry point
ViewDefinition("API.Controllers.OrderController.CreateOrder")
→ Calls OrderService.CreateOrderAsync

// Business logic
ViewDefinition("Services.OrderService.CreateOrderAsync")
→ Calls PaymentService.ProcessPayment

// Infrastructure
ViewDefinition("Services.PaymentService.ProcessPayment")
→ Calls PaymentGateway.ChargeAsync

// External
ViewDefinition("Infrastructure.Gateways.StripeGateway.ChargeAsync")
```

### 4. Identify Patterns

Look for:
- ✅ Consistent naming (Service, Repository, Controller suffixes)
- ✅ Dependency injection patterns
- ✅ Error handling patterns
- ✅ Logging patterns
- ✅ Async/await usage

### 5. Build Mental Map

**Create hierarchy in your mind:**
```
Controllers (API layer)
    ↓
Services (Business logic)
    ↓
Repositories (Data access)
    ↓
Domain Models
```

---

## ⚠️ Common Pitfalls

### Pitfall 1: Skipping LoadSolution

```csharp
❌ WRONG:
ViewDefinition("MyClass.MyMethod")  // FAIL: workspace not initialized

✅ RIGHT:
LoadSolution("project.sln")  // Initialize FIRST
ViewDefinition("MyClass.MyMethod")  // Works
```

### Pitfall 2: Reading Too Much Detail Too Early

```csharp
❌ WRONG: Read every file implementation before understanding structure

✅ RIGHT:
load_projectect(detailLevel: "TypesOnly") → Overview
2. Identify key areas
3. Then drill into specific implementations
```

### Pitfall 3: Not Using Semantic Search

```csharp
❌ WRONG: "I'll browse files until I find payment processing"
   → 30-60 minutes wasted

✅ RIGHT: FindPotentialDuplicates(targetCode: "process payment", threshold: 0.7)
   → 10 seconds
```

### Pitfall 4: Ignoring References

```csharp
❌ WRONG: Only read implementation, ignore usage

✅ RIGHT:view_definitiontion(...)     → Understand what it does
2. FindReferences(...)     → Understand how it's used
   → See real-world usage patterns
```

---

## 📊 Time Comparison

| Task | Manual | With MCP | Speedup |
|------|--------|----------|---------|
| Understand project structure | 30-60 min | 30 sec | **60-120x** |
| Find specific functionality | 20-40 min | 10 sec | **120-240x** |
| Understand implementation | 10-20 min | Instant | **∞** |
| Find all usages | 15-30 min | 5 sec | **180-360x** |
| Map dependencies | 30-60 min | 2-3 min | **15-30x** |
| **TOTAL** | **2-4 hours** | **3-5 min** | **24-48x** |

---

## 🎓 Advanced Techniques

### Technique 1: Architecture Discovery

**Find all repository patterns:**
```csharp
FindPotentialDuplicates(
    targetCode: "public async Task<T> GetByIdAsync<T>(int id) { return await _context.FindAsync<T>(id); }",
    threshold: 0.7
)
```

**Find all service patterns:**
```csharp
FindPotentialDuplicates(
    targetCode: "public class UserService { private readonly IUserRepository _repository; }",
    threshold: 0.7
)
```

### Technique 2: Error Handling Discovery

```csharp
FindPotentialDuplicates(
    targetCode: "try { } catch (Exception ex) { _logger.LogError(ex); throw new ServiceException('Error', ex); }",
    threshold: 0.6
)
```

### Technique 3: Authentication/Authorization Patterns

```csharp
FindPotentialDuplicates(
    targetCode: "[Authorize(Roles = 'Admin')] public async Task<IActionResult> AdminOnly() { }",
    threshold: 0.7
)
```

---

**💡 Key Takeaway:** 2-5 minutes with MCP tools gives you better understanding than 2-4 hours of manual exploration.

**Always start with:** LoadSolutioload_projectject → FindPotentialDuplicates → ViewDefinition → FindReferences
