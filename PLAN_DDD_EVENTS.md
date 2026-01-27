# Domain Events Implementation Plan

## Overview

Domain events decouple side effects from domain operations. Entity raises event → SaveChanges triggers dispatch → Handlers execute in parallel.

---

## Design Decisions

- Events dispatch **post-commit** (after `SavedChangesAsync`)
- Handlers execute **in parallel** (independent side effects)
- Events use **past tense** naming (`UserCreatedEvent`)
- Native AOT compatible via pattern matching

---

## File Structure

| Status | File | Layer |
|--------|------|-------|
| ✅ | `Domain/Shared/Interfaces/IDomainEvent.cs` | Domain |
| ✅ | `Domain/Shared/Interfaces/IAggregateRoot.cs` | Domain |
| ✅ | `Domain/Shared/Models/DomainEvent.cs` | Domain |
| ✅ | `Domain/Shared/Models/Entity.cs` | Domain |
| ⬜ | `Application/Shared/Interfaces/IDomainEventHandler.cs` | Application |
| ⬜ | `Application/Shared/Interfaces/IDomainEventDispatcher.cs` | Application |
| ⬜ | `Application/Shared/Models/DomainEventHandler.cs` | Application |
| ⬜ | `Application/Shared/Services/DomainEventDispatcher.cs` | Application |
| ⬜ | `Infrastructure.EFCore/Adapters/DomainEventInterceptor.cs` | Infrastructure |

---

## Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Event | `{Entity}{Action}Event` | `UserCreatedEvent`, `OrderPlacedEvent` |
| Handler | `{Action}Handler` | `SendWelcomeEmailHandler` |

---

## Implementation

### IDomainEventHandler

**File:** `Application/Shared/Interfaces/IDomainEventHandler.cs`

```csharp
namespace Application.Shared.Interfaces;

public interface IDomainEventHandler
{
    bool CanHandle(IDomainEvent domainEvent);
    ValueTask HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

public interface IDomainEventHandler<in TEvent> : IDomainEventHandler where TEvent : IDomainEvent
{
    ValueTask HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
```

---

### DomainEventHandler

**File:** `Application/Shared/Models/DomainEventHandler.cs`

```csharp
namespace Application.Shared.Models;

public abstract class DomainEventHandler<TEvent> : IDomainEventHandler<TEvent> where TEvent : IDomainEvent
{
    public bool CanHandle(IDomainEvent domainEvent) => domainEvent is TEvent;

    public ValueTask HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (domainEvent is TEvent typedEvent)
        {
            return HandleAsync(typedEvent, cancellationToken);
        }
        return ValueTask.CompletedTask;
    }

    public abstract ValueTask HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
```

---

### IDomainEventDispatcher

**File:** `Application/Shared/Interfaces/IDomainEventDispatcher.cs`

```csharp
namespace Application.Shared.Interfaces;

public interface IDomainEventDispatcher
{
    ValueTask DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    ValueTask DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
```

---

### DomainEventDispatcher

**File:** `Application/Shared/Services/DomainEventDispatcher.cs`

```csharp
namespace Application.Shared.Services;

internal sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IEnumerable<IDomainEventHandler> _handlers;

    public DomainEventDispatcher(IEnumerable<IDomainEventHandler> handlers)
    {
        _handlers = handlers;
    }

    public async ValueTask DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var tasks = _handlers
            .Where(h => h.CanHandle(domainEvent))
            .Select(h => h.HandleAsync(domainEvent, cancellationToken).AsTask());

        await Task.WhenAll(tasks);
    }

    public async ValueTask DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await DispatchAsync(domainEvent, cancellationToken);
        }
    }
}
```

---

### DomainEventInterceptor

**File:** `Infrastructure.EFCore/Adapters/DomainEventInterceptor.cs`

```csharp
namespace Infrastructure.EFCore.Adapters;

internal sealed class DomainEventInterceptor : SaveChangesInterceptor
{
    private readonly IDomainEventDispatcher _dispatcher;

    public DomainEventInterceptor(IDomainEventDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await DispatchEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async ValueTask DispatchEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var entitiesWithEvents = context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var events = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        await _dispatcher.DispatchAsync(events, cancellationToken);
    }
}
```

---

## Registration

**Application layer:**

```csharp
// Application/Shared/Extensions/SharedServiceCollectionExtensions.cs
services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
```

**Feature layer:**

```csharp
// Application.Server/{Feature}/Extensions/{Feature}ServiceCollectionExtensions.cs
services.AddScoped<IDomainEventHandler, MyEventHandler>();
```

**Infrastructure layer:**

```csharp
// Infrastructure.EFCore/Extensions/EFCoreServiceCollectionExtensions.cs
services.AddScoped<DomainEventInterceptor>();

services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.AddInterceptors(sp.GetRequiredService<DomainEventInterceptor>());
});
```

---

## HelloWorld Test Feature

| File | Purpose |
|------|---------|
| `Domain/HelloWorld/Entities/HelloWorldEntity.cs` | Entity raises event |
| `Domain/HelloWorld/Events/HelloWorldCreatedEvent.cs` | Event record |
| `Application.Server/HelloWorld/EventHandlers/HelloWorldCreatedEventHandler.cs` | Logs event |
| `Application.Server/HelloWorld/Extensions/HelloWorldServiceCollectionExtensions.cs` | Registers handler |
| `Infrastructure.EFCore/Configurations/HelloWorldEntityConfiguration.cs` | EF config |
| `Presentation.WebApp.Server/Controllers/HelloWorldController.cs` | Test endpoint |

### HelloWorldEntity

```csharp
namespace Domain.HelloWorld.Entities;

public sealed class HelloWorldEntity : Entity, IAggregateRoot
{
    public string Message { get; private set; }

    public HelloWorldEntity(Guid id, string message) : base(id)
    {
        Message = message;
        AddDomainEvent(new HelloWorldCreatedEvent(Id, Message));
    }
}
```

### HelloWorldCreatedEvent

```csharp
namespace Domain.HelloWorld.Events;

public sealed record HelloWorldCreatedEvent(Guid EntityId, string Message) : DomainEvent;
```

### HelloWorldCreatedEventHandler

```csharp
namespace Application.Server.HelloWorld.EventHandlers;

internal sealed class HelloWorldCreatedEventHandler : DomainEventHandler<HelloWorldCreatedEvent>
{
    private readonly ILogger<HelloWorldCreatedEventHandler> _logger;

    public HelloWorldCreatedEventHandler(ILogger<HelloWorldCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public override ValueTask HandleAsync(HelloWorldCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EVENT HANDLED: EntityId={EntityId}, Message={Message}", domainEvent.EntityId, domainEvent.Message);
        return ValueTask.CompletedTask;
    }
}
```

### HelloWorldController

```csharp
namespace Presentation.WebApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HelloWorldController : ControllerBase
{
    private readonly AppDbContext _context;

    public HelloWorldController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] string message, CancellationToken ct)
    {
        var entity = new HelloWorldEntity(Guid.NewGuid(), message);
        _context.HelloWorlds.Add(entity);
        await _context.SaveChangesAsync(ct);

        return Ok(new { entity.Id, entity.Message });
    }
}
```

---

## Implementation Steps

### Phase 1: Core Infrastructure

1. Create `IDomainEventHandler.cs`
2. Create `IDomainEventDispatcher.cs`
3. Create `DomainEventHandler.cs`
4. Create `DomainEventDispatcher.cs`
5. Update `SharedServiceCollectionExtensions.cs`
6. Create `DomainEventInterceptor.cs`
7. Update `EFCoreServiceCollectionExtensions.cs`
8. Build and verify

### Phase 2: Unit Tests

| File | Tests |
|------|-------|
| `DomainEventDispatcherTests.cs` | Dispatches to matching handlers |
| | Skips non-matching handlers |
| | Dispatches in parallel |
| | Handles empty handler list |
| `DomainEventHandlerTests.cs` | CanHandle returns true for matching type |
| | CanHandle returns false for non-matching type |
| | HandleAsync routes to typed method |

9. Create `Application.UnitTests/Shared/Services/DomainEventDispatcherTests.cs`
10. Create `Application.UnitTests/Shared/Models/DomainEventHandlerTests.cs`
11. Run tests and verify all pass

### Phase 3: HelloWorld Integration Test

12. Create `HelloWorldEntity.cs`
13. Create `HelloWorldCreatedEvent.cs`
14. Create `HelloWorldCreatedEventHandler.cs`
15. Create `HelloWorldServiceCollectionExtensions.cs`
16. Create `HelloWorldEntityConfiguration.cs`
17. Add `DbSet<HelloWorldEntity>` to AppDbContext
18. Create `HelloWorldController.cs`
19. Register in `ServerApplication.cs`
20. Test `POST /api/helloworld` with body `"Test"`
21. Verify log output
