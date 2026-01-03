---
applyTo: '**'
---
# Architecture Rules

## Core Principle: No Jumper Wires, No Duck Tape

**NEVER** add shortcuts, hacks, or workarounds that bypass proper architectural layers. Every piece of code must follow the established patterns, even if it takes more time.

## Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                      PRESENTATION                           │
│  (Blazor, CLI, API Controllers)                            │
│  - Can reference: Application, Domain                       │
│  - CANNOT reference: Infrastructure directly                │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      INFRASTRUCTURE                         │
│  (EFCore, Binance, External Services)                      │
│  - Can reference: Application, Domain                       │
│  - Implements interfaces from Application                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                       APPLICATION                           │
│  (Services, Interfaces, Use Cases)                         │
│  - Can reference: Domain only                               │
│  - Defines abstractions (interfaces)                        │
│  - NO knowledge of infrastructure details                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                         DOMAIN                              │
│  (Entities, Value Objects, Enums)                          │
│  - References: NOTHING (zero external dependencies)         │
│  - Pure C# with no framework dependencies                   │
└─────────────────────────────────────────────────────────────┘
```

## Forbidden Patterns (Jumper Wires / Duck Tape)

### ❌ NEVER DO:

1. **Direct Infrastructure Reference from Presentation**
   ```csharp
   // BAD: Blazor component directly using EF Core
   @inject EFCoreDbContext DbContext
   ```

2. **Concrete Types in Application Layer**
   ```csharp
   // BAD: Application layer knowing about Binance
   public class TradingService
   {
       private readonly BinanceClient _binance; // WRONG!
   }
   ```

3. **Domain Depending on External Libraries**
   ```csharp
   // BAD: Domain entity using Newtonsoft or EF Core attributes
   public class Order
   {
       [JsonProperty("id")]  // WRONG! Domain must be pure
       [Key]                 // WRONG! No EF Core in Domain
       public Guid Id { get; }
   }
   ```

4. **Skipping Layers**
   ```csharp
   // BAD: Presentation directly calling infrastructure
   public class MyComponent
   {
       private readonly BinanceExchangeService _binance; // WRONG!
   }
   ```

5. **Static Service Locator**
   ```csharp
   // BAD: Resolving services statically
   var service = ServiceLocator.Get<IMyService>(); // WRONG!
   ```

6. **Hardcoded Infrastructure Details in Application**
   ```csharp
   // BAD: Connection strings, API URLs in Application layer
   public class MyService
   {
       private const string ApiUrl = "https://api.binance.com"; // WRONG!
   }
   ```

## Required Patterns (Proper Architecture)

### ✅ ALWAYS DO:

1. **Depend on Abstractions**
   ```csharp
   // GOOD: Application layer depends on interface
   public class TradingService
   {
       private readonly IExchangeService _exchange; // Interface, not concrete
   }
   ```

2. **Infrastructure Implements Application Interfaces**
   ```csharp
   // GOOD: Infrastructure implements Application interface
   // In Infrastructure.Binance:
   public class BinanceExchangeService : IExchangeService
   ```

3. **Domain Stays Pure**
   ```csharp
   // GOOD: Domain entity with no external dependencies
   public class Order
   {
       public Guid Id { get; }
       public static Order Create(...) => new Order(...);
   }
   ```

4. **Presentation Uses Application Interfaces**
   ```csharp
   // GOOD: Blazor injects interface
   @inject IExchangeService ExchangeService
   @inject IMarketDataHub MarketDataHub
   ```

5. **Configuration in Infrastructure**
   ```csharp
   // GOOD: Infrastructure handles its own config
   // In Infrastructure.Binance:
   public class BinanceOptions
   {
       public string ApiUrl { get; set; }
   }
   ```

## Ignorance Principles

### Exchange Platform Ignorance
- Application layer has NO knowledge of Binance, Bybit, etc.
- Use `IExchangeService`, `IMarketDataStream` interfaces
- Exchange code is just a string identifier, not a type

### Persistence Ignorance
- Application layer has NO knowledge of EF Core, SQLite, etc.
- Use `IOrderStore`, `IMarketDataStore` interfaces
- No `DbContext`, `DbSet`, or EF attributes in Application/Domain

### UI Framework Ignorance
- Application layer has NO knowledge of Blazor, SignalR, etc.
- Business logic works the same whether called from Blazor, CLI, or API

### Type Variation Ignorance (General Rule)

When you have entities or concepts that come in multiple "types" or "modes" (e.g., Paper vs Live accounts, Spot vs Futures markets, Manual vs Automated orders), apply these rules:

1. **Domain stays unified** - Use a single entity with a `Type` enum, not separate entities per type
2. **Application stays agnostic** - Business logic calls interfaces without checking types
3. **Infrastructure resolves internally** - Only infrastructure knows how to handle each type differently

```csharp
// BAD: Separate entities per type variant
public class PaperBalance { }    // WRONG!
public class LiveBalance { }     // WRONG!
public class SpotOrder { }       // WRONG!
public class FuturesOrder { }    // WRONG!

// BAD: Application layer branching on type
public class MyService
{
    public async Task Execute(Order order)
    {
        if (order.Type == OrderType.X)      // WRONG! Application shouldn't branch
            await _serviceX.Execute(order);
        else
            await _serviceY.Execute(order);
    }
}

// GOOD: Single entity with Type property
public class Balance { public AccountType Type; public decimal Amount; }
public class Order { public OrderType Type; public decimal Quantity; }

// GOOD: Application calls single interface
public class MyService
{
    private readonly IOrderExecutionService _execution;
    
    public async Task Execute(Order order)
    {
        await _execution.ExecuteAsync(order);  // Agnostic - doesn't check type
    }
}

// GOOD: Infrastructure handles type internally
public class OrderExecutionService : IOrderExecutionService
{
    public async Task ExecuteAsync(Order order)
    {
        // Type resolution happens HERE, not in Application
        var handler = ResolveHandler(order.Type);
        await handler.Execute(order);
    }
}
```

**Principle:** If you find yourself creating `TypeAFoo` and `TypeBFoo` entities, or `if (type == X)` checks in Application layer, you're violating ignorance. Unify in Domain, abstract in Application, resolve in Infrastructure.

## When You Think You Need a Shortcut

If you're tempted to add a "quick fix" that bypasses architecture:

1. **STOP** - Don't do it
2. **Ask**: "What interface should exist in Application layer?"
3. **Create** the proper abstraction
4. **Implement** it in the correct Infrastructure project
5. **Inject** via DI in Presentation

**There are no exceptions.** The time spent doing it properly is always less than the time spent fixing architectural debt later.

## Code Review Checklist

Before committing, verify:

- [ ] Domain layer has zero external package references
- [ ] Application layer only references Domain
- [ ] Infrastructure projects implement Application interfaces
- [ ] Presentation injects interfaces, not concrete types
- [ ] No `using Infrastructure.*` in Application layer
- [ ] No framework-specific attributes in Domain entities
- [ ] All external service calls go through abstractions
