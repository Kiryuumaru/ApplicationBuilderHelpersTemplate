# Interface Placement Research

Research on how established .NET Clean Architecture projects place interfaces (repository contracts, external service abstractions, etc.)

---

## Summary

| Project | Repository Interfaces | External Service Interfaces | Layer Name |
|---------|----------------------|----------------------------|------------|
| **Microsoft eShop** | Domain (per aggregate) | - | `Ordering.Domain` |
| **Microsoft eShopOnWeb** | ApplicationCore (unified) | ApplicationCore (unified) | `ApplicationCore` |
| **Jason Taylor CleanArch** | Application | Application | `Application` |
| **Ardalis CleanArch** | Core (via SharedKernel) | Core | `Core` |
| **Amichai Mantinband CleanArch** | Application | Application | `Application` |
| **Kamil Grzybek ModularMonolith** | Domain (per aggregate) | - | `Domain` |

---

## Detailed Findings

### 1. Microsoft eShop (Official .NET Reference - DDD Style)

**Source:** `dotnet/eShop` (latest .NET 8 microservices reference)

**Repository Interfaces: IN DOMAIN**

```
src/Ordering.Domain/
├── SeedWork/
│   ├── IRepository.cs          ← Generic interface (Domain)
│   └── IUnitOfWork.cs          ← Unit of Work (Domain)
├── AggregatesModel/
│   ├── BuyerAggregate/
│   │   └── IBuyerRepository.cs ← Specific repository (Domain)
│   └── OrderAggregate/
│       └── IOrderRepository.cs ← Specific repository (Domain)
```

**Code comment in source:**
```csharp
//This is just the RepositoryContracts or Interface defined at the Domain Layer
//as requisite for the Order Aggregate
```

**Query Interfaces: IN APPLICATION**
```
src/Ordering.API/Application/Queries/
└── IOrderQueries.cs            ← Query interface (Application)
```

**Pattern:** CQRS - Commands through Domain repos, Queries through Application services.

---

### 2. Microsoft eShopOnWeb (Older Reference - Simpler)

**Source:** `dotnet-architecture/eShopOnWeb`

**All Interfaces: IN ApplicationCore (their "Domain")**

```
src/ApplicationCore/
├── Interfaces/
│   ├── IRepository.cs          ← Repository (ApplicationCore)
│   ├── IReadRepository.cs      ← Read repository (ApplicationCore)
│   ├── IBasketService.cs       ← Application service (ApplicationCore)
│   ├── IOrderService.cs        ← Application service (ApplicationCore)
│   ├── IEmailSender.cs         ← External service (ApplicationCore)
│   ├── ITokenClaimsService.cs  ← External service (ApplicationCore)
│   └── IAppLogger.cs           ← Infrastructure (ApplicationCore)
```

**Pattern:** Single layer contains Domain + Application logic. ALL interfaces together.

---

### 3. Jason Taylor CleanArchitecture (Most Popular Template)

**Source:** `jasontaylordev/CleanArchitecture`

**All Interfaces: IN APPLICATION**

```
src/Application/
├── Common/
│   └── Interfaces/
│       ├── IApplicationDbContext.cs  ← Data access (Application)
│       └── IIdentityService.cs       ← External service (Application)
src/Domain/
├── Entities/
├── Events/
├── Enums/
└── (NO interfaces folder)
```

**Pattern:** Domain is pure (entities, value objects, events). Application owns ALL contracts.

---

### 4. Ardalis CleanArchitecture (Steve Smith / NimblePros)

**Source:** `ardalis/CleanArchitecture`

**Layer Structure:**
- `Core` = Domain + Domain Services
- `UseCases` = Application Services (handlers)
- `Infrastructure` = Implementations

**Interfaces: IN CORE (via SharedKernel NuGet)**

```
src/Clean.Architecture.Core/
├── Interfaces/
│   ├── IDeleteContributorService.cs  ← Domain service (Core)
│   └── IEmailSender.cs               ← External service (Core)
├── ContributorAggregate/
│   └── (no specific repository - uses generic)
```

**Generic Repository:** From `Ardalis.SharedKernel` NuGet package:
```csharp
// In SharedKernel library (external)
public interface IRepository<T> : IRepositoryBase<T> where T : class, IAggregateRoot
```

**Pattern:** Generic repository in shared library. Specific interfaces in Core if needed.

---

### 5. Amichai Mantinband CleanArchitecture (YouTube Educator)

**Source:** `amantinband/clean-architecture`

**All Interfaces: IN APPLICATION**

```
src/CleanArchitecture.Application/
├── Common/
│   └── Interfaces/
│       ├── IRemindersRepository.cs   ← Repository (Application)
│       ├── IUsersRepository.cs       ← Repository (Application)
│       ├── IJwtTokenGenerator.cs     ← External service (Application)
│       ├── IDateTimeProvider.cs      ← Infrastructure (Application)
│       └── IAuthorizationService.cs  ← External service (Application)
src/CleanArchitecture.Domain/
├── Reminders/
├── Users/
└── (NO interfaces - only entities, value objects)
```

**Pattern:** Same as Jason Taylor. Domain is pure. Application owns ALL contracts.

---

### 6. Kamil Grzybek Modular Monolith with DDD

**Source:** `kgrzybek/modular-monolith-with-ddd`

**Repository Interfaces: IN DOMAIN (per aggregate)**

```
src/Modules/Meetings/Domain/
├── Meetings/
│   ├── Meeting.cs
│   ├── IMeetingRepository.cs   ← Repository (Domain, next to entity)
│   └── ...
├── Members/
│   ├── Member.cs
│   └── IMemberRepository.cs    ← Repository (Domain, next to entity)
```

**Pattern:** Repository interface lives WITH its aggregate root in Domain.

---

## Analysis

### Two Camps Emerge

**Camp A: All Interfaces in Application**
- Jason Taylor CleanArchitecture
- Amichai Mantinband CleanArchitecture
- Logic: "Domain should be pure - no contracts, just behavior"

**Camp B: Repository Interfaces in Domain**
- Microsoft eShop (official DDD reference)
- Kamil Grzybek Modular Monolith
- Logic: "Repository is part of aggregate boundary - it's how you persist the aggregate"

**Hybrid (eShopOnWeb, Ardalis):**
- eShopOnWeb combines Domain + Application into single "ApplicationCore"
- Ardalis uses external SharedKernel for generic repository

---

## Key Observations

### 1. Microsoft's DDD Projects (eShop, eShopOnContainers)

Microsoft's official DDD reference architectures place **repository interfaces IN DOMAIN**, specifically:
- Generic `IRepository<T>` in `Domain/SeedWork/`
- Specific `I{Aggregate}Repository` next to the aggregate in `Domain/AggregatesModel/{Aggregate}/`

This follows Eric Evans' DDD blue book where the repository is defined by the domain as the mechanism to reconstitute aggregates.

### 2. Popular Templates (Jason Taylor, Amichai)

Popular templates place **all interfaces IN APPLICATION**. Domain is kept "pure" with only:
- Entities
- Value Objects
- Domain Events
- Enums

These templates prioritize simplicity over strict DDD adherence.

### 3. CQRS Pattern (eShop)

When CQRS is used:
- **Commands** go through Domain repository interfaces
- **Queries** have their own interfaces in Application layer

This is the pattern in Microsoft eShop:
```
Domain:      IBuyerRepository, IOrderRepository (write)
Application: IOrderQueries (read)
```

### 4. External Services

| Project | Email, JWT, etc. |
|---------|------------------|
| eShop | Not in Domain |
| eShopOnWeb | ApplicationCore |
| Jason Taylor | Application |
| Ardalis | Core |
| Mantinband | Application |

**Consensus:** External service interfaces (email, auth, APIs) are NOT in Domain. They go in Application or Core (which includes Application-level concerns).

---

## Recommendations

### Recommended Approach: Strict DDD with Clear Separation

**Repository Interfaces → Domain (per aggregate)**
**External Service Interfaces → Application/Outbound**
**Application Service Interfaces → Application/Inbound**

```
Domain/
├── {Feature}/
│   ├── Entities/
│   │   └── {Entity}.cs
│   ├── Interfaces/
│   │   └── I{Feature}Repository.cs   ← Aggregate persistence
│   │   └── I{Feature}UnitOfWork.cs   ← Atomicity boundary

Application/
├── {Feature}/
│   ├── Interfaces/
│   │   ├── Inbound/
│   │   │   └── I{Feature}Service.cs  ← What app offers to Presentation
│   │   └── Outbound/
│   │       └── ITokenProvider.cs     ← External services app needs
│   │       └── IEmailSender.cs       ← External services app needs
```

### Why This Structure

1. **Domain owns persistence abstraction** - Aggregate defines its repository interface
2. **Application owns orchestration dependencies** - Use-cases define what external services they need
3. **Clear responsibility** - Domain = business concepts, Application = use-case coordination
4. **Testable** - Both layers can mock their outbound interfaces independently

---

## Final Verdict

**Microsoft's DDD reference (eShop) puts repository interfaces in Domain.**

The comment in their source code is explicit:
> "This is just the RepositoryContracts or Interface defined at the Domain Layer as requisite for the Order Aggregate"

However, the **most popular community templates** (Jason Taylor, Amichai) put everything in Application.

**The distinction:**
- If you're doing strict DDD with aggregate boundaries → Domain
- If you want simpler separation (pure entities vs everything else) → Application

---

## Authoritative Sources (Blogs, Documentation, Books)

### Uncle Bob's Clean Architecture (2012)

**Source:** [blog.cleancoder.com](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)

**Key Principle: The Dependency Rule**
> "Source code dependencies can only point inwards. Nothing in an inner circle can know anything at all about something in an outer circle."

**Layer Structure (Inside → Outside):**
1. **Entities** (Enterprise Business Rules)
2. **Use Cases** (Application Business Rules)
3. **Interface Adapters** (Controllers, Gateways, Presenters)
4. **Frameworks & Drivers** (Web, UI, DB, Devices)

**Critical Point:**
> "The overriding rule that makes this architecture work is The Dependency Rule."

Uncle Bob's Clean Architecture places **Entities** (Domain) at the center, with **Use Cases** (Application) as the next layer out. The interfaces that Use Cases depend on are defined by the Use Cases layer, NOT by outer layers. This means:
- Use Cases define the interfaces they need (ports)
- Infrastructure implements those interfaces

**Implication for Repository Interfaces:**
Uncle Bob doesn't explicitly state "repositories in Domain" but his architecture shows that inner layers define abstractions that outer layers implement. The question is whether repositories are part of Entities or Use Cases.

---

### Jeffrey Palermo's Onion Architecture (2008)

**Source:** [jeffreypalermo.com](https://jeffreypalermo.com/2008/07/the-onion-architecture-part-1/)

**Key Quote:**
> "The first layer around the Domain Model is typically where we would find interfaces that provide object saving and retrieving behavior, called repository interfaces. The object saving behavior is not in the application core, however, because it typically involves a database. Only the interface is in the application core."

**Layer Structure (Inside → Outside):**
1. **Domain Model** (Entities, Value Objects)
2. **Domain Services** (Repository Interfaces!)
3. **Application Services**
4. **Infrastructure / Tests / UI**

**Critical Point:**
Palermo explicitly places **repository interfaces** in the "Domain Services" layer, which is the "first layer around the Domain Model". This is NOT the Application layer - it's part of the domain core.

**Diagram Description:**
> "The first layer around the Domain Model is typically where we would find interfaces that provide object saving and retrieving behavior, called repository interfaces."

---

### Alistair Cockburn's Hexagonal Architecture (2005)

**Source:** [alistair.cockburn.us](https://alistair.cockburn.us/hexagonal-architecture/)

**Original Pattern Name:** Ports and Adapters

**Key Concept:**
> "The application communicates over ports to external agencies. The word 'port' is supposed to evoke thoughts of ports in an operating system, where any device that adheres to the protocols of a port can be plugged into it."

> "For each external device there is an adapter that converts the API definition to the signals needed by that device and vice versa."

**Primary vs Secondary Ports:**
- **Primary Ports (Driving):** User-side - how the application is driven (UI, tests)
- **Secondary Ports (Driven):** Server-side - what the application drives (database, external services)

**Repository Pattern in Hexagonal:**
In Cockburn's sample code, he creates:
```java
public interface RateRepository {
   double getRate(double amount);
}
```

The interface lives with the application, and adapters implement it. The key insight is that the **application defines the interface** (port), not the infrastructure.

**Implication:**
The "application" in Hexagonal is the entire inner hexagon (Domain + Application logic). Repository interfaces are ports defined by this inner hexagon.

---

### Microsoft Documentation - DDD & Microservices

**Source:** [docs.microsoft.com - .NET Microservices Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice)

**Explicit Recommendation:**

> "It's recommended that you define and place the repository interfaces in the domain model layer so the application layer, such as your Web API microservice, doesn't depend directly on the infrastructure layer where you've implemented the repository classes."

**Layer Definitions from Microsoft:**

1. **Domain Model Layer:** "The domain model layer is where the business is expressed... It must be totally decoupled from persistence."

2. **Application Layer:** "Defines the jobs the software is supposed to do and directs the expressive domain objects to work out problems."

3. **Infrastructure Layer:** "How the data that was initially held in domain entities is persisted in databases."

**Microsoft's Position on Repository Interfaces:**

> "The Repository pattern is a Domain-Driven Design pattern intended to keep persistence concerns outside of the system's domain model. One or more persistence abstractions - interfaces - are defined in the domain model."

From [Design the infrastructure persistence layer](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design):

> "One or more persistence abstractions - interfaces - are defined in the domain model, and those interfaces are implemented as adapters in the infrastructure."

---

### DevIQ - Repository Pattern

**Source:** [deviq.com/design-patterns/repository-pattern](https://deviq.com/design-patterns/repository-pattern/)

**Key Points:**

> "The Repository pattern is a Domain-Driven Design pattern intended to keep persistence concerns outside of the system's domain model."

> "One or more persistence abstractions - interfaces - are defined in the domain model, and those abstractions have implementations in the form of persistence-specific adapters defined elsewhere in the application."

> "You should only support repositories on your aggregate root objects. If you allow direct data access to members of aggregates, you break the encapsulation the aggregate provides and potentially can corrupt its internal state."

**Explicit Placement:**
> "interfaces - are defined in the domain model"

---

### Vaadin DDD Tutorial (2024)

**Source:** [vaadin.com/blog/ddd-part-3-domain-driven-design-and-the-hexagonal-architecture](https://vaadin.com/blog/ddd-part-3-domain-driven-design-and-the-hexagonal-architecture)

**Repository Location:**

In their code examples:
```java
var customer = customerRepository.findById(input.getCustomerId());
```

> "The application service looks up an aggregate root from a repository in the domain model."

> "We invoke a domain repository to save the customer"

**Terminology:** They refer to `customerRepository` as a "domain repository" - it's part of the domain model.

**Port Placement:**
> "The interface either lives in your application service layer (a factory interface) or your domain model (a repository interface)."

This explicitly distinguishes:
- **Factory interfaces:** Application service layer
- **Repository interfaces:** Domain model

---

## Summary: What Authoritative Sources Say

| Source | Repository Interfaces |
|--------|----------------------|
| **Uncle Bob (Clean Arch)** | Inner layers define abstractions |
| **Jeffrey Palermo (Onion)** | "First layer around Domain Model" (Domain Services) |
| **Alistair Cockburn (Hexagonal)** | Part of the "inner hexagon" (Application defines ports) |
| **Microsoft Docs** | **"Define and place repository interfaces in the domain model layer"** |
| **DevIQ** | **"interfaces are defined in the domain model"** |
| **Vaadin DDD Tutorial** | **"repository interface lives in your domain model"** |

---

## Definitive Answer

**Repository interfaces belong in the Domain layer. Application-level service interfaces belong in Application layer.**

The authoritative sources are clear:

1. **Microsoft's official documentation** explicitly states: "It's recommended that you define and place the repository interfaces in the domain model layer"

2. **Onion Architecture** (Palermo) places "repository interfaces" in the "first layer around the Domain Model"

3. **DDD Pattern definition** (DevIQ): "interfaces - are defined in the domain model"

4. **Hexagonal Architecture** distinguishes repository interfaces (Domain) from factory interfaces (Application)

The popular community templates (Jason Taylor, Amichai) that put interfaces in Application are **pragmatic simplifications**, not strict DDD adherence.

---

## Important Clarification: Two Types of Outbound Interfaces

The research focuses on **repository interfaces** (aggregate persistence). However, there's a distinct category of **Application-level outbound interfaces** that legitimately belong in Application.

### Interface Placement by Type

| Interface Type | Layer | Purpose |
|---------------|-------|---------|
| **Repository Interfaces** | Domain | Reconstitute/persist aggregates |
| **Application Outbound** | Application | External services for use-cases |
| **Application Inbound** | Application | What the app offers to Presentation |

### Why Repositories → Domain

- Define how to **reconstitute aggregates** from storage
- Part of the **aggregate boundary** (per DDD)
- Domain needs to express "I can be saved/loaded" without knowing HOW
- Repository is the **abstraction of persistence for domain concepts**

### Why External Services → Application

- **Use-case driven**, not aggregate-driven
- Support **orchestration** of business operations
- Application layer decides WHEN and WHY to use them
- Examples: token generation, email sending, SMS, external APIs

### Concrete Examples

```
Domain/Identity/Interfaces/
└── IUserRepository.cs              ← Aggregate persistence (Domain)
└── IIdentityUnitOfWork.cs          ← Atomicity boundary (Domain)

Application/Identity/Interfaces/
├── Inbound/
│   ├── IIdentityService.cs         ← Login, register (what app offers)
│   └── IUserTokenSessionService.cs ← Session management (what app offers)
└── Outbound/
    ├── ITokenProvider.cs           ← Generate/validate tokens (external)
    ├── IPasswordHasher.cs          ← Hash passwords (external)
    └── IEmailSender.cs             ← Send verification emails (external)
```

### The Mental Model

**Domain Outbound (Repository):** "I am an Order. I need to be saved and loaded. I don't care how."

**Application Outbound (Service):** "To complete this use-case, I need to send an email, generate a token, and call an external API. I don't care which providers do this."

### CQRS Consideration

When using CQRS:
- **Commands** → Use Domain repositories (write model)
- **Queries** → Use Application query interfaces (read model, bypasses domain)

---

## Repositories Researched

| Repository | Stars | Last Updated |
|------------|-------|--------------|
| dotnet/eShop | 6.5k+ | Active (2024) |
| dotnet-architecture/eShopOnWeb | 10k+ | Active (2024) |
| jasontaylordev/CleanArchitecture | 17k+ | Active (2024) |
| ardalis/CleanArchitecture | 16k+ | Active (2024) |
| amantinband/clean-architecture | 5k+ | Active (2024) |
| kgrzybek/modular-monolith-with-ddd | 11k+ | Active (2024) |

---

## Authoritative Sources Consulted

| Source | Author | Year |
|--------|--------|------|
| The Clean Architecture | Robert C. Martin (Uncle Bob) | 2012 |
| The Onion Architecture | Jeffrey Palermo | 2008 |
| Hexagonal Architecture | Alistair Cockburn | 2005 |
| .NET Microservices Architecture | Microsoft Docs | 2024 |
| Repository Pattern | DevIQ | 2024 |
| DDD and Hexagonal Architecture | Vaadin (Petter Holmström) | 2024 |
| Domain-Driven Design (Blue Book) | Eric Evans | 2003 |

---

## Domain Event Dispatcher Research

### How Projects Implement Event Dispatching

| Project | Interface | Implementation | Location |
|---------|-----------|----------------|----------|
| **Microsoft eShop** | `IMediator` (MediatR) | Extension method on IMediator | Infrastructure |
| **Jason Taylor** | `IMediator` (MediatR) | `DispatchDomainEventsInterceptor` | Infrastructure |
| **Ardalis** | `IDomainEventDispatcher` | `MediatorDomainEventDispatcher` | SharedKernel (external NuGet) |
| **Modular Monolith** | `IDomainEventsDispatcher` | `DomainEventsDispatcher` | BuildingBlocks/Infrastructure |

### Key Finding: Most Use MediatR Directly

Most projects don't define their own `IDomainEventDispatcher` interface. They:
1. Use MediatR's `IMediator.Publish()` directly
2. Create an EF Core interceptor in Infrastructure
3. The interceptor calls `IMediator.Publish()` for each event

**Microsoft eShop approach:**
```csharp
// Infrastructure/MediatorExtension.cs
static class MediatorExtension
{
    public static async Task DispatchDomainEventsAsync(this IMediator mediator, OrderingContext ctx)
    {
        var domainEntities = ctx.ChangeTracker
            .Entries<Entity>()
            .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any());

        var domainEvents = domainEntities
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();

        domainEntities.ToList()
            .ForEach(entity => entity.Entity.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
            await mediator.Publish(domainEvent);
    }
}
```

**Jason Taylor approach:**
```csharp
// Infrastructure/Data/Interceptors/DispatchDomainEventsInterceptor.cs
public class DispatchDomainEventsInterceptor : SaveChangesInterceptor
{
    private readonly IMediator _mediator;
    
    public async Task DispatchDomainEvents(DbContext? context)
    {
        // ... get events from tracked entities ...
        foreach (var domainEvent in domainEvents)
            await _mediator.Publish(domainEvent);
    }
}
```

**Modular Monolith approach (custom interface):**
```csharp
// BuildingBlocks/Infrastructure/DomainEventsDispatching/IDomainEventsDispatcher.cs
public interface IDomainEventsDispatcher
{
    Task DispatchEventsAsync();
}

// BuildingBlocks/Infrastructure/DomainEventsDispatching/DomainEventsDispatcher.cs
public class DomainEventsDispatcher : IDomainEventsDispatcher
{
    private readonly IMediator _mediator;
    // ... implementation using MediatR under the hood ...
}
```

### Interface Location Summary

| Approach | Interface | Interface Location | Implementation Location |
|----------|-----------|-------------------|------------------------|
| **MediatR Direct** | `IMediator` (external) | MediatR NuGet | N/A (use directly) |
| **Custom Wrapper** | `IDomainEventsDispatcher` | Infrastructure | Infrastructure |
| **SharedKernel** | `IDomainEventDispatcher` | External NuGet | External NuGet |

### Conclusion: Dispatcher Interface Location

**Most projects don't define a dispatcher interface at all** - they use MediatR directly.

When a custom interface IS defined (like Modular Monolith), it's placed in **Infrastructure** (or a shared BuildingBlocks/Infrastructure package), NOT in Application or Domain.

**Why Infrastructure?**
- The dispatcher is tightly coupled to the dispatch mechanism (MediatR, in-memory, message queue)
- It's an implementation detail of HOW events get to handlers
- Application doesn't need to know about dispatching - handlers are auto-discovered via DI

### Handlers Location

Handlers (`INotificationHandler<TEvent>`) are always in **Application** layer:

```
Application/{Feature}/EventHandlers/
├── SendWelcomeEmailHandler.cs       ← Handles UserCreatedEvent
├── UpdateInventoryHandler.cs        ← Handles OrderPlacedEvent
└── NotifyWarehouseHandler.cs        ← Handles OrderShippedEvent
```

### Revised Architecture

Based on research, the simplest approach:

| Component | Location | Notes |
|-----------|----------|-------|
| `IDomainEvent` | Domain | Marker interface |
| `DomainEvent` | Domain | Base record |
| `{Action}Event` | Domain/{Feature}/Events/ | Concrete events |
| Dispatch mechanism | Infrastructure | Interceptor + MediatR |
| `{Action}Handler` | Application/{Feature}/EventHandlers/ | Handles events |

**No separate `IDomainEventDispatcher` interface needed** if using MediatR directly.
| dotnet-architecture/eShopOnWeb | 10k+ | Active |
| jasontaylordev/CleanArchitecture | 18k+ | Active |
| ardalis/CleanArchitecture | 16k+ | Active |
| amantinband/clean-architecture | 3k+ | Active |
| kgrzybek/modular-monolith-with-ddd | 11k+ | 2023 |
