# Request Handler Flow Implementation Plan

## Overview

Mediator pattern routes requests to handlers through a pipeline. Controller sends request → Mediator finds handler → Pipeline behaviors wrap execution → Handler returns result.

---

## Design Decisions

- Custom lightweight implementation (no MediatR dependency)
- One handler per request (1:1 mapping)
- Pipeline behaviors run in registration order
- `Unit` type for void returns
- Native AOT compatible via pattern matching

---

## File Structure

| Status | File | Layer |
|--------|------|-------|
| ⬜ | `Application/Shared/Interfaces/IRequest.cs` | Application |
| ⬜ | `Application/Shared/Interfaces/IRequestHandler.cs` | Application |
| ⬜ | `Application/Shared/Interfaces/IPipelineBehavior.cs` | Application |
| ⬜ | `Application/Shared/Interfaces/IMediator.cs` | Application |
| ⬜ | `Application/Shared/Models/Unit.cs` | Application |
| ⬜ | `Application/Shared/Models/RequestHandler.cs` | Application |
| ⬜ | `Application/Shared/Services/Mediator.cs` | Application |

---

## Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Request | `{Action}{Entity}Request` | `CreateUserRequest` |
| Query | `Get{Entity}Request` | `GetUserRequest` |
| Handler | `{Request}Handler` | `CreateUserHandler` |
| Behavior | `{Concern}Behavior` | `LoggingBehavior` |

---

## Implementation

### IRequest

**File:** `Application/Shared/Interfaces/IRequest.cs`

```csharp
namespace Application.Shared.Interfaces;

public interface IRequest<out TResponse>
{
}

public interface IRequest : IRequest<Unit>
{
}
```

---

### Unit

**File:** `Application/Shared/Models/Unit.cs`

```csharp
namespace Application.Shared.Models;

public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Value = new();

    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
    public override string ToString() => "()";

    public static bool operator ==(Unit left, Unit right) => true;
    public static bool operator !=(Unit left, Unit right) => false;
}
```

---

### IRequestHandler

**File:** `Application/Shared/Interfaces/IRequestHandler.cs`

```csharp
namespace Application.Shared.Interfaces;

public interface IRequestHandler
{
    bool CanHandle(object request);
    ValueTask<object?> HandleAsync(object request, CancellationToken cancellationToken = default);
}

public interface IRequestHandler<in TRequest, TResponse> : IRequestHandler
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
```

---

### RequestHandler

**File:** `Application/Shared/Models/RequestHandler.cs`

```csharp
namespace Application.Shared.Models;

public abstract class RequestHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public bool CanHandle(object request) => request is TRequest;

    public async ValueTask<object?> HandleAsync(object request, CancellationToken cancellationToken = default)
    {
        if (request is TRequest typedRequest)
        {
            return await HandleAsync(typedRequest, cancellationToken);
        }
        throw new InvalidOperationException($"Cannot handle request of type {request.GetType().Name}");
    }

    public abstract ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
```

---

### IPipelineBehavior

**File:** `Application/Shared/Interfaces/IPipelineBehavior.cs`

```csharp
namespace Application.Shared.Interfaces;

public interface IPipelineBehavior
{
    ValueTask<TResponse> HandleAsync<TResponse>(
        object request,
        Func<ValueTask<TResponse>> next,
        CancellationToken cancellationToken = default);
}
```

---

### IMediator

**File:** `Application/Shared/Interfaces/IMediator.cs`

```csharp
namespace Application.Shared.Interfaces;

public interface IMediator
{
    ValueTask<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
```

---

### Mediator

**File:** `Application/Shared/Services/Mediator.cs`

```csharp
namespace Application.Shared.Services;

internal sealed class Mediator : IMediator
{
    private readonly IEnumerable<IRequestHandler> _handlers;
    private readonly IEnumerable<IPipelineBehavior> _behaviors;

    public Mediator(
        IEnumerable<IRequestHandler> handlers,
        IEnumerable<IPipelineBehavior> behaviors)
    {
        _handlers = handlers;
        _behaviors = behaviors;
    }

    public ValueTask<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        var handler = _handlers.FirstOrDefault(h => h.CanHandle(request))
            ?? throw new InvalidOperationException($"No handler registered for {request.GetType().Name}");

        Func<ValueTask<TResponse>> pipeline = async () =>
        {
            var result = await handler.HandleAsync(request, cancellationToken);
            return (TResponse)result!;
        };

        foreach (var behavior in _behaviors.Reverse())
        {
            var next = pipeline;
            pipeline = () => behavior.HandleAsync(request, next, cancellationToken);
        }

        return pipeline();
    }
}
```

---

## Registration

**Application layer:**

```csharp
// Application/Shared/Extensions/SharedServiceCollectionExtensions.cs
services.AddScoped<IMediator, Mediator>();
```

**Feature layer:**

```csharp
// Application.Server/{Feature}/Extensions/{Feature}ServiceCollectionExtensions.cs
services.AddScoped<IRequestHandler, MyRequestHandler>();
```

**Behaviors (optional):**

```csharp
// Order matters - first registered runs first (outermost)
services.AddScoped<IPipelineBehavior, LoggingBehavior>();
services.AddScoped<IPipelineBehavior, ValidationBehavior>();
```

---

## HelloWorld Test Feature

| File | Purpose |
|------|---------|
| `Application/HelloWorld/Requests/SayHelloRequest.cs` | Request record |
| `Application.Server/HelloWorld/Handlers/SayHelloHandler.cs` | Returns greeting |
| `Application.Server/HelloWorld/Extensions/HelloWorldServiceCollectionExtensions.cs` | Registers handler |
| `Presentation.WebApp.Server/Controllers/HelloController.cs` | Test endpoint |

### SayHelloRequest

```csharp
namespace Application.HelloWorld.Requests;

public sealed record SayHelloRequest(string Name) : IRequest<string>;
```

### SayHelloHandler

```csharp
namespace Application.Server.HelloWorld.Handlers;

internal sealed class SayHelloHandler : RequestHandler<SayHelloRequest, string>
{
    private readonly ILogger<SayHelloHandler> _logger;

    public SayHelloHandler(ILogger<SayHelloHandler> logger)
    {
        _logger = logger;
    }

    public override ValueTask<string> HandleAsync(SayHelloRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("HANDLER EXECUTED: Name={Name}", request.Name);
        return ValueTask.FromResult($"Hello, {request.Name}!");
    }
}
```

### HelloController

```csharp
namespace Presentation.WebApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HelloController : ControllerBase
{
    private readonly IMediator _mediator;

    public HelloController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> SayHello(string name, CancellationToken ct)
    {
        var result = await _mediator.SendAsync(new SayHelloRequest(name), ct);
        return Ok(new { Greeting = result });
    }
}
```

---

## Implementation Steps

### Phase 1: Core Infrastructure

1. Create `IRequest.cs`
2. Create `Unit.cs`
3. Create `IRequestHandler.cs`
4. Create `RequestHandler.cs`
5. Create `IPipelineBehavior.cs`
6. Create `IMediator.cs`
7. Create `Mediator.cs`
8. Update `SharedServiceCollectionExtensions.cs`
9. Build and verify

### Phase 2: Unit Tests

| File | Tests |
|------|-------|
| `MediatorTests.cs` | Routes to correct handler |
| | Throws when no handler registered |
| | Pipeline behaviors wrap handler |
| | Behaviors run in registration order |
| `RequestHandlerTests.cs` | CanHandle returns true for matching type |
| | CanHandle returns false for non-matching type |
| `UnitTests.cs` | Equality and GetHashCode |

10. Create `Application.UnitTests/Shared/Services/MediatorTests.cs`
11. Create `Application.UnitTests/Shared/Models/RequestHandlerTests.cs`
12. Create `Application.UnitTests/Shared/Models/UnitTests.cs`
13. Run tests and verify all pass

### Phase 3: HelloWorld Integration Test

14. Create `SayHelloRequest.cs`
15. Create `SayHelloHandler.cs`
16. Create `HelloWorldServiceCollectionExtensions.cs`
17. Create `HelloController.cs`
18. Register in `ServerApplication.cs`
19. Test `GET /api/hello/World`
20. Verify response `{"greeting":"Hello, World!"}`
