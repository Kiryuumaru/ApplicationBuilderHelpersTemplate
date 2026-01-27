---
applyTo: '**'
---
# File Structure Rules

## One Type Per File

Each file contains exactly one public type. File name matches the type name.

| Type Kind | File Name Format | Example |
|-----------|------------------|---------|
| Class | `{ClassName}.cs` | `UserService.cs` |
| Interface | `I{Name}.cs` | `IUserService.cs` |
| Record | `{RecordName}.cs` | `LoginRequest.cs` |
| Enum | `{EnumName}.cs` | `OrderStatus.cs` |

**Exceptions:**
- Private nested types within the containing type's file
- File-scoped types using `file` modifier

---

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes, Records, Structs | PascalCase | `UserService`, `LoginRequest` |
| Interfaces | `I` + PascalCase | `IUserService`, `ITokenProvider` |
| Methods | PascalCase | `GetUserAsync`, `ValidateToken` |
| Properties | PascalCase | `UserId`, `IsAuthenticated` |
| Private fields | `_camelCase` | `_userService`, `_logger` |
| Local variables | camelCase | `userId`, `tokenResult` |
| Constants | PascalCase | `DefaultTimeout`, `MaxRetries` |
| Parameters | camelCase | `userId`, `cancellationToken` |
| Async methods | Suffix `Async` | `GetUserAsync`, `SaveAsync` |

---

## Folder Structure

### Domain Layer

Domain references DI helpers only (`Microsoft.Extensions.DependencyInjection.Abstractions`, `ApplicationBuilderHelpers`, `Domain.SourceGenerators`). Contains pure business logic, entities, and value objects.

`Interfaces/` in Domain contains marker interfaces only (`IDomainEvent`, `IAggregateRoot`), not ports.

```
Domain/
+-- Shared/
|   +-- Interfaces/              <- Marker interfaces only (not ports)
|   +-- Extensions/
+-- {Feature}/
    +-- Entities/
    +-- ValueObjects/
    +-- Enums/
    +-- Events/
    +-- Constants/
    +-- Services/                <- Domain services (pure logic, Singleton)
    +-- Exceptions/
```

### Application Layer

Application references Domain only. MAY reference core abstractions (`Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Hosting.Abstractions`) and DI helpers.

- `Interfaces/In/` - Incoming ports (callable by any layer, implemented by Application*)
- `Interfaces/Out/` - Outgoing ports (callable by Application*/Infrastructure*, implemented by Infrastructure*)
- `Interfaces/` - Internal abstractions (callable by Application* only)

```
Application/
+-- Shared/
|   +-- Interfaces/              <- Internal abstractions
|   |   +-- In/                  <- Incoming ports
|   |   +-- Out/                 <- Outgoing ports
|   +-- Models/
|   +-- Services/                <- Application services
|   +-- Extensions/
+-- {Feature}/
    +-- Interfaces/
    |   +-- In/
    |   +-- Out/
    +-- Services/
    +-- Models/
    +-- Workers/                 <- Background entry points
    +-- Validators/
    +-- EventHandlers/
    +-- Extensions/
```

### Application.Server Layer

Application.Server references Application and Domain. MAY reference the same core abstractions and DI helpers as Application. Contains server-specific logic.

MUST NOT reference Application.Client.

```
Application.Server/
+-- Shared/
|   +-- Interfaces/
|   |   +-- In/
|   |   +-- Out/
+-- {Feature}/
    +-- Interfaces/
    |   +-- In/
    |   +-- Out/
    +-- Services/
    +-- Models/
    +-- Workers/                 <- Server-only workers
    +-- EventHandlers/
```

### Application.Client Layer

Application.Client references Application and Domain. MAY reference the same core abstractions and DI helpers as Application. Contains client-specific logic.

MUST NOT reference Application.Server.

```
Application.Client/
+-- Shared/
|   +-- Interfaces/
|   |   +-- In/
|   |   +-- Out/
+-- {Feature}/
    +-- Interfaces/
    |   +-- In/
    |   +-- Out/
    +-- Services/
    +-- Models/
    +-- EventHandlers/
```

### Infrastructure Layer

Infrastructure references Application and Domain. Implements Interfaces/Out ports.

MUST NOT reference Presentation.

```
Infrastructure.{Provider}/
+-- Adapters/                    <- Outgoing adapters (implement Interfaces/Out)
+-- Services/
+-- Repositories/
+-- Configurations/
+-- Extensions/
+-- Models/

Infrastructure.{Provider}.{Feature}/
+-- Adapters/
+-- Repositories/
+-- Configurations/
+-- Extensions/
```

### Presentation Layer

Presentation references Application and Domain. Drives the application through Interfaces/In ports.

MUST NOT reference Infrastructure except in Program.cs for DI registration.

MUST NOT call Interfaces/Out directly.

```
Presentation/
+-- Commands/
|   +-- BaseCommand.cs
+-- Contracts/
|   +-- {Feature}/
|       +-- Requests/
|       +-- Responses/
|       +-- Hubs/
+-- Shared/
    +-- Components/
    +-- Extensions/

Presentation.WebApp/
+-- Commands/
|   +-- BaseWebAppCommand.cs
+-- Components/
|   +-- Layout/
|   +-- Pages/
|   +-- Shared/
+-- Services/
+-- Models/

Presentation.WebApp.Server/
+-- Program.cs                   <- Composition root (Infrastructure wiring here only)
+-- Commands/
|   +-- MainCommand.cs
+-- Controllers/                 <- Incoming adapters
+-- Extensions/

Presentation.WebApp.Client/
+-- Program.cs                   <- Composition root
+-- Commands/
|   +-- MainCommand.cs
+-- Components/                  <- Incoming adapters (Blazor)
|   +-- Layout/
|   +-- Pages/
|   +-- Shared/
+-- Services/
+-- Models/

Presentation.WebApi/
+-- Program.cs                   <- Composition root
+-- Commands/
|   +-- MainCommand.cs
+-- Controllers/V{n}/            <- Incoming adapters
+-- Models/
|   +-- Requests/
|   +-- Responses/
+-- Middleware/
+-- Filters/

Presentation.Cli/
+-- Program.cs                   <- Composition root
+-- Commands/
|   +-- MainCommand.cs
+-- Services/
```

---

## ApplicationDependency and Serialization Structure

```
Domain/
+-- Domain.cs                               <- ApplicationDependency
+-- Serialization/
|   +-- DomainJsonContext.cs
|   +-- Converters/
+-- Shared/
|   +-- Extensions/
|       +-- SharedServiceCollectionExtensions.cs
+-- {Feature}/
    +-- Extensions/
    |   +-- {Feature}ServiceCollectionExtensions.cs
    +-- Services/
    +-- Entities/
    +-- ValueObjects/

Application/
+-- Application.cs                          <- ApplicationDependency
+-- Serialization/
|   +-- ApplicationJsonContext.cs
+-- Shared/
|   +-- Extensions/
|       +-- SharedServiceCollectionExtensions.cs
+-- {Feature}/
    +-- Extensions/
    |   +-- {Feature}ServiceCollectionExtensions.cs
    +-- ...

Application.Server/
+-- ServerApplication.cs                    <- ApplicationDependency
+-- Serialization/
|   +-- ApplicationServerJsonContext.cs
+-- {Feature}/
    +-- Extensions/
        +-- {Feature}ServiceCollectionExtensions.cs

Application.Client/
+-- ClientApplication.cs                    <- ApplicationDependency
+-- Serialization/
|   +-- ApplicationClientJsonContext.cs
+-- {Feature}/
    +-- Extensions/
        +-- {Feature}ServiceCollectionExtensions.cs

Infrastructure.{Name}/
+-- {Name}Infrastructure.cs                 <- ApplicationDependency
+-- Serialization/
|   +-- {Name}JsonContext.cs
+-- Extensions/
    +-- {Name}ServiceCollectionExtensions.cs
```

---

## Member Ordering Within Files

1. Constants
2. Static fields
3. Instance fields
4. Constructors
5. Properties
6. Public methods
7. Private methods

---

## Namespace Convention

Namespace mirrors folder path from `src/`.

```
src/Application/Identity/Services/UserService.cs
-> namespace Application.Identity.Services;
```

---

## Type Placement by Kind

Files MUST be placed in folders matching their type kind.

| Type Kind | Required Folder | Example |
|-----------|-----------------|---------|
| Interface (internal) | `Interfaces/` | `Interfaces/IDomainEventDispatcher.cs` |
| Interface (In) | `Interfaces/In/` | `Interfaces/In/IOrderService.cs` |
| Interface (Out) | `Interfaces/Out/` | `Interfaces/Out/IOrderRepository.cs` |
| Enum | `Enums/` | `Enums/OrderStatus.cs` |
| Record (DTO/Model) | `Models/` | `Models/LoginRequest.cs` |
| Service class | `Services/` | `Services/UserService.cs` |
| Adapter class | `Adapters/` | `Adapters/BinanceExchangeAdapter.cs` |
| Repository class | `Repositories/` | `Repositories/OrderRepository.cs` |
| Entity | `Entities/` | `Entities/User.cs` |
| Value object | `ValueObjects/` | `ValueObjects/Email.cs` |
| Exception | `Exceptions/` | `Exceptions/UserNotFoundException.cs` |
| Extension class | `Extensions/` | `Extensions/ServiceCollectionExtensions.cs` |
| Validator | `Validators/` | `Validators/LoginRequestValidator.cs` |
| Configuration | `Configurations/` | `Configurations/UserConfiguration.cs` |
| Constants class | `Constants/` | `Constants/ErrorMessages.cs` |
| Application Worker | `Workers/` | `Workers/TradeFiller.cs` |
| Presentation Worker Host | `Workers/` | `Workers/TradeFillerHost.cs` |
| Domain Event | `Events/` | `Events/OrderPlacedEvent.cs` |
| Event Handler | `EventHandlers/` | `EventHandlers/SendWelcomeEmailHandler.cs` |
| ApplicationDependency | Project root | `Application.cs` |
| ServiceCollectionExtensions | `Extensions/` | `Extensions/LocalStoreServiceCollectionExtensions.cs` |
| ConfigurationExtensions | `Extensions/` | `Extensions/EFCoreSqliteConfigurationExtensions.cs` |
| JsonContext | `Serialization/` | `Serialization/DomainJsonContext.cs` |
| JsonConverter | `Serialization/Converters/` | `Serialization/Converters/CamelCaseEnumConverter.cs` |
| Command | `Commands/` | `Commands/MainCommand.cs` |

**Verification:**
- Before creating a file, identify its type kind
- Place in the corresponding folder within the feature/component area
- If folder does not exist, create it
