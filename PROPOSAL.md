# Architecture Proposal: Domain Interfaces Model

## Status

**APPROVED** - Ready to implement

---

## Summary

Move **repository and unit of work interfaces** from `Application/{Feature}/Interfaces/Outbound/` to `Domain/{Feature}/Interfaces/`.

Keep **external service interfaces** (token providers, email senders, etc.) in `Application/{Feature}/Interfaces/Outbound/`.

This aligns with the mental model:
- **Domain** = Drawings (what things ARE, aggregate persistence contracts)
- **Application** = Flow (orchestration, external service needs, use cases)
- **Infrastructure** = How (implementation details)

---

## Current State

```
Domain/
  {Feature}/
    Entities/
    ValueObjects/
    Events/
    Services/              <- Pure domain logic

Application/
  {Feature}/
    Interfaces/
      Inbound/             <- Services offered to outer layers
      Outbound/            <- Repository/UoW contracts (CURRENT)
    Services/              <- Orchestration

Infrastructure/
  Repositories/            <- Implements Application.Outbound
```

**Problem:** Repository interfaces are "drawings" of persistence contracts. They belong with the entities they persist.

---

## Proposed State

```
Domain/
  Shared/
    Interfaces/
      IUnitOfWork.cs                    <- Base UoW contract
  {Feature}/
    Entities/
    ValueObjects/
    Events/
    Services/                           <- Pure domain logic
    Interfaces/
      I{Feature}Repository.cs           <- Repository contract (MOVED HERE)
      I{Feature}UnitOfWork.cs           <- Feature UoW contract (MOVED HERE)

Application/
  Shared/
    Interfaces/
      IDomainEventDispatcher.cs         <- Internal: dispatch events (one)
      IDomainEventHandler.cs            <- Internal: handle events (many)
    Interfaces/Outbound/
      IEmailSender.cs                   <- Shared external service
      IDateTimeProvider.cs              <- Shared infrastructure utility
  {Feature}/
    Interfaces/                         <- Internal coordination (if needed)
    Interfaces/Inbound/
      I{Feature}Service.cs              <- Services offered to outer layers
    Interfaces/Outbound/
      ITokenProvider.cs                 <- Feature-specific external service
      IPasswordHasher.cs                <- Feature-specific external service
    Services/                           <- Orchestration

Infrastructure/
  Repositories/                         <- Implements Domain.Interfaces
  Services/                             <- Implements Application.Interfaces/Outbound
  Adapters/
    DomainEventInterceptor.cs           <- Calls IDomainEventDispatcher after commit
```

---

## Mental Model

| Layer | Role | Contains |
|-------|------|----------|
| **Domain** | Drawings | Entities, ValueObjects, Events, Repository Interfaces, UoW Interfaces, Pure Services, Exceptions |
| **Application** | Flow | Services (orchestration), Handlers (reactions), Workers (background), External Service Interfaces |
| **Infrastructure** | How | Repository implementations, External service implementations, DbContexts |
| **Presentation** | Entry | Controllers, Components, Commands |

### Interface Ownership Principle

- **Domain owns persistence abstraction** - "I am an aggregate. I can be saved and loaded."
- **Application owns external service abstraction** - "My use-cases need tokens, emails, APIs."

---

## Interface Location Rules

### Domain Layer Interfaces

| Location | Purpose | Implemented By |
|----------|---------|----------------|
| `Domain/Shared/Interfaces/` | Base contracts (`IUnitOfWork`) | Infrastructure |
| `Domain/{Feature}/Interfaces/` | Feature persistence contracts (`I{Feature}Repository`) | Infrastructure |

Domain interfaces define **aggregate persistence** - how to save/load domain objects.

### Application Layer Interfaces

| Location | Purpose | Implemented By |
|----------|---------|----------------|
| `Application/Shared/Interfaces/` | Internal coordination (`IDomainEventHandler`) | Application |
| `Application/Shared/Interfaces/Outbound/` | Shared external services (`IEmailSender`) | Infrastructure |
| `Application/{Feature}/Interfaces/` | Internal feature coordination | Application |
| `Application/{Feature}/Interfaces/Inbound/` | Services exposed to outer layers | Application |
| `Application/{Feature}/Interfaces/Outbound/` | Feature external services (`ITokenProvider`) | Infrastructure |

**Key Distinction:**
- **Domain.Interfaces** = Aggregate persistence (repositories, unit of work)
- **Application.Interfaces/Outbound** = External services for use-cases (tokens, email, APIs)

### Summary Table

| Interface Type | Location | Who Calls | Who Implements |
|----------------|----------|-----------|----------------|
| Repository | `Domain/{Feature}/Interfaces/` | Application | Infrastructure |
| UnitOfWork | `Domain/{Feature}/Interfaces/` | Application | Infrastructure |
| DomainEventDispatcher | `Application/Shared/Interfaces/` | Infrastructure | Application |
| DomainEventHandler | `Application/Shared/Interfaces/` | Application (Dispatcher) | Application |
| Feature Service (Inbound) | `Application/{Feature}/Interfaces/Inbound/` | Presentation | Application |
| External Service (Outbound) | `Application/{Feature}/Interfaces/Outbound/` | Application | Infrastructure |
| Internal Coordinator | `Application/{Feature}/Interfaces/` | Application | Application |

### Domain Event Flow

```
Entity.AddDomainEvent()
        │
        ▼
UnitOfWork.CommitAsync()
        │
        ▼
Infrastructure: DomainEventInterceptor (after SaveChanges)
        │
        ▼
Application: IDomainEventDispatcher.DispatchAsync()  ← ONE dispatcher
        │
        ▼
Application: IDomainEventHandler.HandleAsync()       ← MANY handlers
```

**One Dispatcher, Many Handlers:**
- `IDomainEventDispatcher` - Single service that routes events
- `IDomainEventHandler` - Multiple handlers registered for different event types

### Why Two Types of Outbound?

| Type | Layer | Reason |
|------|-------|--------|
| **Repository** | Domain | Defines how to persist/reconstitute **aggregates** |
| **External Service** | Application | Defines what **use-cases** need from external systems |

**Example:**
- `IUserRepository` (Domain) - "Users need to be saved and loaded"
- `ITokenProvider` (Application) - "Login use-case needs to generate tokens"

---

## Dependency Flow

```
Presentation
     │
     ▼
Infrastructure ──────────────────┐
     │                           │
     ▼                           │
Application                      │
     │                           │
     ▼                           │
Domain ◄─────────────────────────┘
     (Infrastructure implements Domain interfaces)
```

**Key:** Infrastructure depends on Domain for interface contracts, not Application.

---

## Platform-Specific Domains

For projects with `Domain.Server` / `Domain.Client`:

```
Domain/
  HelloWorld/
    Interfaces/
      IHelloWorldRepository.cs       <- Shared contract

Domain.Server/
  HelloWorld/
    Interfaces/
      IHelloWorldServerRepository.cs <- Extends shared, adds server methods
  AuditLog/
    Interfaces/
      IAuditLogRepository.cs         <- Server-only feature

Domain.Client/
  HelloWorld/
    Interfaces/
      IHelloWorldClientRepository.cs <- Extends shared, adds client methods
  OfflineSync/
    Interfaces/
      IOfflineSyncRepository.cs      <- Client-only feature
```

Platform-specific interfaces extend base interfaces:

```csharp
// Domain/HelloWorld/Interfaces/IHelloWorldRepository.cs
public interface IHelloWorldRepository
{
    Task<HelloWorldEntity?> GetByIdAsync(Guid id);
    void Add(HelloWorldEntity entity);
}

// Domain.Server/HelloWorld/Interfaces/IHelloWorldServerRepository.cs
public interface IHelloWorldServerRepository : IHelloWorldRepository
{
    Task<IReadOnlyList<HelloWorldEntity>> GetAllForAdminAsync();
}

// Domain.Client/HelloWorld/Interfaces/IHelloWorldClientRepository.cs
public interface IHelloWorldClientRepository : IHelloWorldRepository
{
    Task<IReadOnlyList<HelloWorldEntity>> GetPendingSyncAsync();
}
```

---

## Edge Cases & Solutions

### 1. Projection/DTO Queries

**Problem:** Need `GetOrderSummariesAsync()` returning `OrderSummaryDto`, not entity.

**Solution:** CQRS pattern - separate query services in Application.

```
Domain/{Feature}/Interfaces/
  IOrderRepository.cs              <- Commands (entities)

Application/{Feature}/Interfaces/Inbound/
  IOrderQueryService.cs            <- Queries (DTOs, projections)
```

### 2. Cross-Aggregate Queries

**Problem:** "Get orders with user names" joins across aggregates.

**Solution:** Read models in Application, not Domain repositories.

```csharp
// Application/Reporting/Interfaces/Inbound/IOrderReportService.cs
public interface IOrderReportService
{
    Task<IReadOnlyList<OrderWithUserDto>> GetOrdersWithUsersAsync();
}
```

### 3. Bulk Operations

**Problem:** `DeleteAllExpiredAsync()` bypasses domain events.

**Solution:** Document that bulk operations don't raise events, or create Infrastructure-only interface.

```csharp
// Domain/Trading/Interfaces/IOrderRepository.cs
public interface IOrderRepository
{
    // Standard operations (raise events via UoW)
    void Add(OrderEntity entity);
    
    // Bulk operation - NO domain events raised
    Task<int> DeleteExpiredAsync(DateTimeOffset before);
}
```

### 4. External Data in Domain Services

**Problem:** Domain pricing service needs exchange rates.

**Solution:** Pass data as parameter, keeping Domain pure.

```csharp
// Domain service receives data, doesn't fetch it
public Money CalculateTotal(Order order, ExchangeRate rate)
{
    return order.Amount * rate.Value;
}

// Application fetches rate, calls domain service
var rate = await _exchangeRateService.GetRateAsync(currency);
var total = _pricingService.CalculateTotal(order, rate);
```

### 5. Multi-Tenancy

**Problem:** Every query needs tenant filtering.

**Solution:** Tenant is Infrastructure implementation detail. Domain interface stays clean.

```csharp
// Domain interface - no tenant
public interface IOrderRepository
{
    Task<OrderEntity?> GetByIdAsync(Guid id);
}

// Infrastructure implementation - tenant injected
internal class OrderRepository(DbContext db, ICurrentTenant tenant) : IOrderRepository
{
    public async Task<OrderEntity?> GetByIdAsync(Guid id)
    {
        return await db.Orders
            .Where(o => o.TenantId == tenant.Id)  // Implicit filtering
            .FirstOrDefaultAsync(o => o.Id == id);
    }
}
```

---

## CQRS Pattern (Recommended)

For complex applications, separate command and query paths:

| Path | Interface Location | Returns | Purpose |
|------|-------------------|---------|---------|
| Command | `Domain/{Feature}/Interfaces/` | Entities | Create, Update, Delete |
| Query | `Application/{Feature}/Interfaces/Inbound/` | DTOs | Read, Search, Report |

```
Domain/Trading/Interfaces/
  IOrderRepository.cs           <- Add, Update, Delete, GetById

Application/Trading/Interfaces/Inbound/
  IOrderService.cs              <- PlaceOrder, CancelOrder (orchestration)
  IOrderQueryService.cs         <- GetOrderSummaries, SearchOrders (queries)
```

---

## Migration Plan

### Phase 1: Move Interfaces

1. Create `Domain/{Feature}/Interfaces/` folders
2. Move `I{Feature}Repository.cs` from Application to Domain
3. Move `I{Feature}UnitOfWork.cs` from Application to Domain
4. Update namespaces
5. Update all references

### Phase 2: Update Infrastructure References

1. Change Infrastructure project references from Application to Domain (for interfaces)
2. Infrastructure still references Application for any Inbound services it needs

### Phase 3: Reorganize Application.Outbound

1. Keep `Application/{Feature}/Interfaces/Outbound/` for **external service interfaces**
2. Ensure only repositories moved to Domain, not external services
3. Update architecture rules to clarify the distinction

### Phase 4: Update Rules

1. Update `architecture.instructions.md` with new interface locations
2. Add "Drawings/Flow/How" mental model documentation
3. Document CQRS pattern for queries

---

## Files to Change (Plain Template)

### Move to Domain

| From | To |
|------|-----|
| `Application/HelloWorld/Interfaces/Outbound/IHelloWorldRepository.cs` | `Domain/HelloWorld/Interfaces/IHelloWorldRepository.cs` |
| `Application/HelloWorld/Interfaces/Outbound/IHelloWorldUnitOfWork.cs` | `Domain/HelloWorld/Interfaces/IHelloWorldUnitOfWork.cs` |
| `Application/Shared/Interfaces/IUnitOfWork.cs` | `Domain/Shared/Interfaces/IUnitOfWork.cs` |

### Update References

| File | Change |
|------|--------|
| `Application/HelloWorld/Services/HelloWorldService.cs` | Update using statements |
| `Infrastructure.InMemory/Repositories/InMemoryHelloWorldRepository.cs` | Update using statements |
| `Infrastructure.InMemory/InMemoryInfrastructure.cs` | Update using statements |

### Delete

| File |
|------|
| `Application/HelloWorld/Interfaces/Outbound/` (empty folder) |
| `Application/Shared/Interfaces/IUnitOfWork.cs` (moved) |

---

## Validation Checklist

- [ ] Domain remains free of framework dependencies (EF Core, etc.)
- [ ] Domain interfaces only reference Domain types (entities, value objects)
- [ ] Application orchestrates using Domain interfaces
- [ ] Infrastructure implements Domain interfaces
- [ ] Presentation only calls Application.Inbound interfaces
- [ ] Build succeeds with 0 warnings
- [ ] All tests pass
- [ ] Cross-context communication uses Application.Inbound services

---

## Resolved Questions

1. **Should `IDomainEventDispatcher` be in Domain or Application?**
   - **Decision:** Application
   - **Reason:** It's called by Infrastructure (after SaveChanges) and routes to Application handlers. It's orchestration (flow), not a persistence contract (drawing). Infrastructure calling Application interfaces is the standard Clean Architecture pattern.

2. **Should `IDomainEventHandler` be in Domain or Application?**
   - **Decision:** Application
   - **Reason:** Handlers contain orchestration/flow logic, not persistence contracts. They react to events by performing side effects.

3. **Should pure Domain services be injectable or static?**
   - **Decision:** Injectable as Singleton
   - **Reason:** Consistent DI pattern, allows testing with mocks if needed, no runtime overhead.

---

## Interface Summary After Migration

| Interface Type | Location | Who Calls | Who Implements |
|----------------|----------|-----------|----------------|
| Repository | `Domain/{Feature}/Interfaces/` | Application | Infrastructure |
| UnitOfWork (base) | `Domain/Shared/Interfaces/` | - | - |
| UnitOfWork (feature) | `Domain/{Feature}/Interfaces/` | Application | Infrastructure |
| DomainEventDispatcher | `Application/Shared/Interfaces/` | Infrastructure | Application |
| DomainEventHandler | `Application/Shared/Interfaces/` | Application (Dispatcher) | Application |
| Feature Service (Inbound) | `Application/{Feature}/Interfaces/Inbound/` | Presentation | Application |
| External Service (Outbound) | `Application/{Feature}/Interfaces/Outbound/` | Application | Infrastructure |

---

## Approval

- [x] Architecture validated
- [x] Edge cases addressed
- [x] Migration plan approved
- [x] Ready to implement
