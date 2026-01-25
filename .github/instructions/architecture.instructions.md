---
applyTo: '**'
---
# Architecture Rules

## Layer Dependencies

```
Domain
  ^
Application
  ^
Application.Server    Application.Client
  ^                     ^
Infrastructure.*      Infrastructure.*
  ^                     ^
Presentation.*.Server Presentation.*.Client
```

```
+-------------------------------------------+
|            PRESENTATION                   |
|  References: Application, Domain          |
|  Infrastructure: Program.cs only          |
+-------------------------------------------+
                    |
                    v
+-------------------------------------------+
|           INFRASTRUCTURE                  |
|  References: Application, Domain          |
|  Implements: Application interfaces       |
+-------------------------------------------+
                    |
                    v
+-------------------------------------------+
|            APPLICATION                    |
|  References: Domain only                  |
|  Defines: Interfaces, services            |
+-------------------------------------------+
                    |
                    v
+-------------------------------------------+
|              DOMAIN                       |
|  References: Nothing                      |
|  Contains: Entities, value objects        |
+-------------------------------------------+
```

---

## Layer Reference Rules

Domain:
- MUST NOT reference any other layer (Application, Infrastructure, or Presentation)
- Is the innermost circle containing pure business logic
- MUST NOT have framework attributes or external dependencies

Application:
- MUST only reference Domain
- MUST NOT reference Infrastructure or Presentation
- Defines ports and orchestrates business logic

Application.Server:
- MUST reference Application and Domain
- MUST NOT reference Application.Client
- Contains server-specific logic

Application.Client:
- MUST reference Application and Domain
- MUST NOT reference Application.Server
- Contains client-specific logic

Application.Server and Application.Client are parallel branches, not hierarchical.

Infrastructure:
- MUST reference Application and Domain
- MUST NOT reference Presentation
- Implements ports defined in Application

Presentation:
- MUST reference Application and Domain
- MUST NOT reference Infrastructure except in Program.cs
- Drives the application through ports

---

## Ports and Adapters

Ports are interfaces that define boundaries.

Ports/In (incoming ports):
- Define what the application offers to the outside world
- Are implemented by Application services
- Are called by Presentation layer
- Examples: `IOrderService`, `IIdentityService`, `IMarketStreamerService`

Ports/Out (outgoing ports):
- Define what the application needs from the outside world
- Are implemented by Infrastructure adapters
- Are called by Application services
- Examples: `IOrderRepository`, `IBrokerPort`, `IMarketDataPort`

Incoming adapters:
- Drive the application
- Live in Presentation layer
- Call Ports/In
- Examples: Controllers, Worker hosts, Blazor components

Outgoing adapters:
- Are driven by the application
- Live in Infrastructure layer
- Implement Ports/Out
- Examples: Repositories, HTTP clients, message bus clients

---

## Entry Points vs Services

Entry points:
- Are the initiators of application logic
- Create scopes and resolve services
- Are not injectable
- Do not have DI lifetime in the traditional sense

Entry points include:
- Controllers - HTTP request entry point
- Application Workers - Background task entry point
- Blazor Components - UI interaction entry point
- Queue Consumers - Message entry point
- Event Listeners - Event entry point

Services:
- Are injectable units of logic
- Are registered with DI lifetime
- Are resolved by entry points or other services

Services include:
- Domain Services - Pure business logic (Singleton)
- Application Services - Orchestration with I/O (Scoped)
- Repositories - Data access (Scoped)

---

## Service Categories

### Domain Services

Domain services:
- Contain pure business logic
- MUST NOT perform I/O
- MUST NOT depend on repositories, HTTP clients, or external services
- Receive all required data as method parameters
- Return calculated results
- MUST be registered as Singleton
- Are stateless calculators
- Produce same output for same input

Examples: `OrderPricingService`, `RiskAssessmentService`, `PasswordStrengthService`.

### Application Services

Application services:
- Orchestrate business operations
- Implement Ports/In interfaces
- Call Domain services for business logic
- Call Ports/Out for I/O operations
- Coordinate transactions through IUnitOfWork
- MUST be registered as Scoped
- Depend on Scoped services like DbContext, ICurrentUser, and IUnitOfWork

Examples: `OrderService`, `IdentityService`, `MarketStreamerService`.

---

## Application Workers

Application workers:
- Are background entry points
- Are internal classes
- Are not injectable
- Consume services but are not consumed by others
- Are like Controllers but for background tasks instead of HTTP requests
- Create scopes and resolve services within those scopes
- Handle their own execution loop, scheduling, or event listening
- Contain business logic for background operations
- Call Application services and Ports/Out directly

Worker locations by scope:
- `Application/{Feature}/Workers/` - Workers that run on all platforms (server and client)
- `Application.Server/{Feature}/Workers/` - Server-only workers
- `Application.Client/{Feature}/Workers/` - Client-only workers

Examples: `TradeFiller`, `MarketListingSync`, `StaleAnonymousUserCleanup`, `OrderPlacedListener`.

---

## Presentation Worker Hosts

Worker hosts:
- Are BackgroundService wrappers in Presentation layer
- Live in `Presentation.WebApp.Server/Workers/`
- Are Singleton because .NET hosting requires one instance for the application lifetime
- Handle the timer loop and error logging
- Create scopes and call Application workers within those scopes
- Contain no business logic
- Only manage WHEN to run
- Delegate WHAT to do to Application workers

Examples: `TradeFillerHost`, `MarketListingSyncHost`, `StaleAnonymousUserCleanupHost`.

---

## Internal Interfaces

Internal interfaces:
- Are abstractions used within Application and Infrastructure layers only
- MUST NOT be used by Presentation layer directly

Internal interfaces locations:
- Shared internal interfaces MUST be in `Application/Shared/Interfaces/`
- Server shared internal interfaces MUST be in `Application.Server/Shared/Interfaces/`
- Client shared internal interfaces MUST be in `Application.Client/Shared/Interfaces/`
- Feature-specific internal interfaces MUST be in `Application/{Feature}/Interfaces/`

Examples: `IAuthenticatedHttpClientFactory`, `IEventHandler<T>`, `IOfflineSyncManager`.

When Presentation needs functionality that uses internal interfaces:
- Create a Ports/In service that uses the internal interface
- Presentation calls the Ports/In service
- Presentation never touches the internal interface directly

---

## Composition Root

Infrastructure wiring occurs only in `Program.cs` of executable projects.

```
Presentation.WebApp/Program.cs
Presentation.Cli/Program.cs
```

Controllers, components, services, and commands MUST NOT import Infrastructure namespaces.

---

## Abstraction Requirements

- Application services MUST depend on interfaces, not concrete types
- Infrastructure MUST implement Application interfaces
- Presentation MUST inject interfaces via DI

---

## Service Accessibility

Interfaces: `public`
Implementations: `internal sealed`

```csharp
public interface ILocalStoreService { }
internal sealed class IndexedDBLocalStoreService : ILocalStoreService { }
```

---

## Dependency Injection Lifetime Rules

Singleton:
- MUST only inject other Singleton services
- MUST NOT inject Scoped services (captive dependency causes stale data, wrong user context, and concurrency bugs)
- When needing request-specific data, MUST pass data as method parameters

Scoped:
- MAY inject Singleton and Scoped services

Transient:
- MAY inject Singleton, Scoped, and Transient services

Entry points like Controllers and Application Workers do not follow these rules because they are not injectable. They create scopes and resolve services from those scopes.

### Lifetimes by Layer

Domain services MUST be Singleton. Domain services are stateless calculators with pure logic.

Application services MAY be Singleton, Scoped, or Transient depending on their dependencies and state requirements.

Infrastructure adapters are typically Scoped. Infrastructure adapters depend on DbContext for repositories. Infrastructure adapters depend on HttpClient for external APIs.

Infrastructure utilities MAY be Singleton. Examples: clock adapters, configuration providers, stateless factories.

---

## Infrastructure Separation

Providers (SQLite, Postgres) and Features (LocalStore, Identity) are independent.

- Provider projects MAY reference Application and Domain
- Provider projects MUST NOT reference Feature projects
- Feature projects MAY reference Application, Domain, and `IDbContextFactory<T>`
- Feature projects MUST NOT reference Provider projects
- Composition happens at Presentation layer only

Infrastructure naming convention is `Infrastructure.{FeatureCollection}.{SpecificCategory}`.

Feature collection projects:
- Implement repository interfaces for a specific bounded context
- Are ignorant of database providers
- Examples: `Infrastructure.EFCore.Server.Identity`, `Infrastructure.EFCore.Server.Trading`

Provider projects:
- Configure specific database or service providers
- Are ignorant of features
- Examples: `Infrastructure.EFCore.Sqlite`, `Infrastructure.EFCore.Postgres`

---

## Ignorance Principles

- Application and Domain MUST have no knowledge of EF Core, SQLite, or databases
- Application MUST have no knowledge of specific external services (Binance, etc.)
- Application MUST have no knowledge of Blazor, SignalR, or HTTP
- Domain MUST use single entity with Type enum, not separate entities per variant

---

## Naming Ignorance

Application layer classes reference only the abstractions they depend on. Infrastructure implementations are named after their specific technology.

- Application class depending on `ILocalStoreService` MUST be named `LocalStoreTokenStorage` (not `EFCoreTokenStorage`)
- Infrastructure class implementing `ILocalStoreService` MUST be named after implementation: `IndexedDBLocalStoreService`
- Infrastructure class implementing `IExchangeService` MUST be named after implementation: `BinanceExchangeService`

---

## Search Before Create

Before creating any type, utility, or pattern:

1. MUST search the codebase for existing types with similar purpose
2. MUST check these locations in order:
   - `Domain/Shared/` for domain primitives and interfaces
   - `Domain/{Feature}/ValueObjects/` for domain value types
   - `Domain/{Feature}/Services/` for domain logic
   - `Application/Shared/Models/` for shared DTOs and results
   - `Application/{Feature}/Models/` for feature-specific DTOs
   - `Application.Server/{Feature}/` for server-specific types
   - `Application.Client/{Feature}/` for client-specific types
3. If found: MUST use it or extend it
4. If not found: MUST create in the appropriate shared location

---

## One Concept, One Type, One Location

- If same type defined in multiple files, MUST keep one definition and delete others
- If same concept has different names, MUST consolidate to single canonical name
- If private type could be shared, MUST move to appropriate shared location
- If anonymous type used for known concept, MUST use the existing named type

---

## No Duplication

MUST extract when:
- Same logic appears 2+ times
- Same pattern emerges across files
- Same constant value used in multiple locations
- Same error handling repeated

Shared code locations by layer:

- Domain interfaces MUST go in `Domain/Shared/Interfaces/`
- Domain services MUST go in `Domain/{Feature}/Services/`
- Domain value objects MUST go in `Domain/{Feature}/ValueObjects/`
- Domain logic MUST go in `Domain/Shared/` or `Domain/{Feature}/`
- Application ports MUST go in `Application/{Feature}/Ports/In/` or `Application/{Feature}/Ports/Out/`
- Application services MUST go in `Application/{Feature}/Services/`
- Application models MUST go in `Application/{Feature}/Models/`
- Application shared internal interfaces MUST go in `Application/Shared/Interfaces/`
- Application feature-specific internal interfaces MUST go in `Application/{Feature}/Interfaces/`
- Application utilities MUST go in `Application/Shared/` or `Application/{Feature}/Extensions/`
- Application workers MUST go in `Application/{Feature}/Workers/`, `Application.Server/{Feature}/Workers/`, or `Application.Client/{Feature}/Workers/`
- Infrastructure adapters MUST go in `Infrastructure.{Provider}.{Feature}/Adapters/`
- Infrastructure utilities MUST go in `Infrastructure.{Provider}/Extensions/`
- Presentation shared code MUST go in `Presentation/Shared/`
- Presentation contracts MUST go in `Presentation/Contracts/{Feature}/`
- Presentation worker hosts MUST go in `Presentation.WebApp.Server/Workers/`
- Test helpers MUST go in base test class or `TestHelpers/`

---

## Constants Over Magic Values

Every literal value used more than once MUST be a named constant.

- Domain constants MUST be in `Domain/{Feature}/Constants/`
- Application settings MUST be in Configuration or `Application/{Feature}/Constants/`
- Test values MUST be in test base class or constants file

---

## Consolidation Workflow

When duplicates are discovered:

1. MUST identify canonical location based on layer rules (prefer `Application/Models` or `Domain/ValueObjects`)
2. MUST keep the most complete definition
3. MUST update all references to use the shared type
4. MUST delete duplicate definitions
5. MUST verify build succeeds

---

## Prohibited Patterns

- NEVER use `@inject DbContext` in components
- NEVER use `using Infrastructure.*` outside Program.cs
- NEVER use concrete types in Application constructor parameters
- NEVER use framework attributes (`[Key]`, `[JsonProperty]`) in Domain
- NEVER use static service locator patterns
- NEVER hardcode connection strings or URLs in Application
- NEVER use `if (type == X)` branching in Application layer
- NEVER define Ports/In in Infrastructure layer
- NEVER define Ports/Out in Infrastructure layer
- NEVER implement Ports/In in Infrastructure layer
- NEVER implement Ports/Out in Application layer
- NEVER call Ports/Out directly from Presentation layer
- NEVER call Infrastructure directly from Presentation layer (except DI registration)
- NEVER place business logic in Presentation worker hosts
- NEVER place I/O operations in Domain services
- NEVER expose internal interfaces to Presentation layer
- NEVER inject Application Workers into other services
- NEVER copy-paste with minor variations
- NEVER use inline magic values (hardcoded strings, numbers, timeouts)
- NEVER duplicate validation logic
- NEVER repeat error handling patterns
- NEVER use anonymous types when named types exist
- NEVER place shared types in server-only or client-only projects when both need them
---

## ApplicationDependency

Every layer (except Presentation) MUST have an ApplicationDependency implementation at project root.

| Layer | Class Name | Location |
|-------|------------|----------|
| Domain | `Domain` | `Domain/Domain.cs` |
| Application | `Application` | `Application/Application.cs` |
| Application.Server | `ServerApplication` | `Application.Server/ServerApplication.cs` |
| Application.Client | `ClientApplication` | `Application.Client/ClientApplication.cs` |
| Infrastructure.* | `{Name}Infrastructure` | `Infrastructure.{Name}/{Name}Infrastructure.cs` |

ApplicationDependency rules:
- MUST extend `ApplicationDependency` directly
- MUST NOT extend another ApplicationDependency implementation
- MUST register services through ServiceCollectionExtensions
- MUST NOT register services directly in ApplicationDependency
- MUST call `base.Method()` in all lifecycle method overrides

Examples: `Domain`, `Application`, `ServerApplication`, `ClientApplication`, `EFCoreInfrastructure`, `EFCoreSqliteInfrastructure`, `IdentityInfrastructure`, `OpenTelemetryInfrastructure`.

---

## ApplicationDependency Lifecycle

Lifecycle methods execute in order:

1. `CommandPreparation(ApplicationBuilder)` - Before command argument parsing
2. `BuilderPreparation(ApplicationHostBuilder)` - After argument parsing, before host builder creation
3. `AddConfigurations(ApplicationHostBuilder, IConfiguration)` - After builder creation
4. `AddServices(ApplicationHostBuilder, IServiceCollection)` - After configuration
5. `AddMiddlewares(ApplicationHost, IHost)` - After DI registration
6. `AddMappings(ApplicationHost, IHost)` - After middleware
7. `RunPreparation(ApplicationHost)` - After mappings (parallel across layers)
8. `RunPreparationAsync(ApplicationHost, CancellationToken)` - After mappings (parallel across layers)

Method usage:
- `CommandPreparation` - Add custom type parsers
- `BuilderPreparation` - Prepare host builder
- `AddConfigurations` - Add configuration providers, bind IOptions
- `AddServices` - Register DI via ServiceCollectionExtensions only
- `AddMiddlewares` - Configure middleware pipeline
- `AddMappings` - Map endpoints, routes, SignalR hubs
- `RunPreparation` - Synchronous initialization/bootstrap
- `RunPreparationAsync` - Asynchronous initialization (database setup, etc.)

---

## ServiceCollectionExtensions

Every feature and Shared MUST have its own internal ServiceCollectionExtensions class.

ServiceCollectionExtensions rules:
- MUST be `internal static` class
- MUST have extension methods for IServiceCollection
- MUST be named `{Feature}ServiceCollectionExtensions`
- MUST have methods named `Add{Feature}Services`
- MAY have `Add{Feature}Configurations` or `Add{Feature}Middlewares`

Shared ServiceCollectionExtensions:
- MUST be in `{Layer}/Shared/Extensions/SharedServiceCollectionExtensions.cs`
- MUST have method named `AddSharedServices`
- MAY have `AddSharedConfigurations` or `AddSharedMiddlewares`

```csharp
namespace Application.LocalStore.Extensions;

internal static class LocalStoreServiceCollectionExtensions
{
    internal static IServiceCollection AddLocalStoreServices(this IServiceCollection services)
    {
        services.AddScoped<ILocalStoreFactory, LocalStoreFactory>();
        return services;
    }
}
```

---

## ConfigurationExtensions

ConfigurationExtensions enable cross-layer configuration sharing.

ConfigurationExtensions rules:
- MUST be `public static` class
- MUST have extension methods for IConfiguration
- MUST be named `{Feature}ConfigurationExtensions`
- MUST use private const string for key names
- MUST have `Get{Key}` method to retrieve value
- MUST have `Set{Key}` method to store value
- MAY use `GetRefValue` for reference chain support (`@ref:` prefix)

```csharp
namespace Infrastructure.EFCore.Sqlite.Extensions;

public static class EFCoreSqliteConfigurationExtensions
{
    private const string SqliteConnectionStringKey = "SQLITE_CONNECTION_STRING";

    public static string GetSqliteConnectionString(this IConfiguration configuration)
    {
        return configuration.GetRefValue(SqliteConnectionStringKey);
    }

    public static void SetSqliteConnectionString(this IConfiguration configuration, string connectionString)
    {
        configuration[SqliteConnectionStringKey] = connectionString;
    }
}
```

---

## Serialization

Each layer MAY have a `Serialization/` folder for source-generated JSON contexts (Native AOT support).

JsonContext rules:
- MUST be `partial class` extending `JsonSerializerContext`
- MUST use `[JsonSourceGenerationOptions]` with `GenerationMode = Metadata`
- MUST use `[JsonSerializable(typeof(T))]` for each type to serialize
- MUST be named `{Layer}JsonContext`
- MUST be `public` if shared across layers (Domain, Application)
- MUST be `internal` if layer-internal only (Infrastructure)

| Layer | Class Name | Accessibility |
|-------|------------|---------------|
| Domain | `DomainJsonContext` | `public` |
| Application | `ApplicationJsonContext` | `public` |
| Application.Server | `ApplicationServerJsonContext` | `public` |
| Application.Client | `ApplicationClientJsonContext` | `public` |
| Infrastructure.{Name} | `{Name}JsonContext` | `internal` |

Converters:
- MUST be in `Serialization/Converters/` subfolder
- MUST be named `{Name}JsonConverter`
- MUST extend `JsonConverter<T>`

```csharp
namespace Domain.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(Role))]
public partial class DomainJsonContext : JsonSerializerContext
{
}
```

---

## BuildConstantsGenerator

Presentation leaf projects MUST enable BuildConstantsGenerator for build constants and command wiring.

csproj configuration:

```xml
<PropertyGroup>
    <GenerateBuildConstants>true</GenerateBuildConstants>
    <BaseCommandType>Presentation.WebApp.Commands.BaseWebAppCommand</BaseCommandType>
</PropertyGroup>
```

| Property | Required | Description |
|----------|----------|-------------|
| `GenerateBuildConstants` | Yes | Set to `true` to enable generation |
| `BaseCommandType` | No | Fully qualified name of intermediate base command |

BaseCommandType by project family:

| Project Family | BaseCommandType |
|----------------|-----------------|
| WebApp.Server | `Presentation.WebApp.Commands.BaseWebAppCommand` |
| WebApp.Client | `Presentation.WebApp.Commands.BaseWebAppCommand` |
| WebApi | `Presentation.Commands.BaseCommand` or custom |
| Cli | `Presentation.Commands.BaseCommand` or custom |

Generated types in `Build` namespace:
- `Build.Constants` - Static build constants
- `Build.ApplicationConstants` - `IApplicationConstants` implementation
- `Build.BaseCommand<T>` - Extends `BaseCommandType` with `ApplicationConstants` wired

`IApplicationConstants` is registered in DI by `BaseCommand.AddServices()`. Inject anywhere via constructor or `@inject`.

---

## Command Hierarchy

```
Presentation.Commands.BaseCommand<T>               <- Manual (shared)
         ^
Presentation.WebApp.Commands.BaseWebAppCommand<T>  <- Manual (optional intermediate)
         ^
Build.BaseCommand<T>                               <- Generated per-project
         ^
MainCommand                                        <- Manual (leaf command)
```

Commands:
- MUST be internal classes
- MUST extend `Build.BaseCommand<TBuilder>`
- MUST have `[Command]` attribute with description
- MUST override `ApplicationBuilder()` to create appropriate builder type
- MAY override lifecycle methods for command-specific DI
- MAY have `[CommandOption]` for CLI options
- MAY have `[CommandArgument]` for positional arguments

| Command Type | Class Name | Attribute |
|--------------|------------|-----------|
| Main/Default | `MainCommand` | `[Command("Main subcommand.")]` |
| Sub-command | `{Name}Command` | `[Command("{name}", description: "...")]` |
| Nested | `{Parent}{Child}Command` | `[Command("{parent} {child}", description: "...")]` |

---

## Command Attributes

`[Command]` - Applied to classes to define command metadata.

| Property | Type | Description |
|----------|------|-------------|
| `Term` | `string?` | Command name (e.g., `"migrate"`, `"serve"`) |
| `Description` | `string?` | Help text for the command |

Constructors:
- `[Command("Description only")]` - Default/main command
- `[Command("name", description: "Description")]` - Named subcommand

`[CommandOption]` - Applied to properties to define CLI options (flags).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Term` | `string?` | - | Long option name (`--log-level`) |
| `ShortTerm` | `char?` | - | Short option name (`-l`) |
| `EnvironmentVariable` | `string?` | - | Env var to read from |
| `Required` | `bool` | `false` | Whether required |
| `Description` | `string?` | - | Help text |
| `FromAmong` | `object[]` | `[]` | Allowed values |
| `CaseSensitive` | `bool` | `false` | Case sensitivity for allowed values |

Required can be specified via:
- C# `required` keyword on property
- `Required = true` in attribute

`[CommandArgument]` - Applied to properties to define positional arguments.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Name` | `string?` | - | Argument name |
| `Description` | `string?` | - | Help text |
| `Position` | `int` | `0` | Positional order |
| `Required` | `bool` | `false` | Whether required |
| `FromAmong` | `object[]` | `[]` | Allowed values |
| `CaseSensitive` | `bool` | `false` | Case sensitivity for allowed values |

```csharp
[CommandOption('l', "log-level", EnvironmentVariable = "LOG_LEVEL", Description = "Level of logs to show.")]
public LogLevel LogLevel { get; set; } = LogLevel.Information;

[CommandOption("credentials-override", EnvironmentVariable = "CREDENTIALS_OVERRIDE")]
public string? CredentialsOverrideBase64 { get; set; }

[CommandOption("api-key", Description = "API key for authentication.")]
public required string ApiKey { get; set; }
```

---

## Program.cs Structure

Program.cs MUST:
- Create ApplicationBuilder using `ApplicationBuilder.Create()`
- Add ApplicationDependency implementations via `.AddApplication<T>()`
- Add Commands via `.AddCommand<T>()`
- Call `.RunAsync(args)`

```csharp
return await ApplicationBuilder.Create()
    .AddApplication<Domain>()
    .AddApplication<Application>()
    .AddApplication<ServerApplication>()
    .AddApplication<IdentityInfrastructure>()
    .AddApplication<EFCoreInfrastructure>()
    .AddApplication<EFCoreSqliteInfrastructure>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
```

---

## Prohibited Patterns (ApplicationBuilderHelpers)

- NEVER register services directly in ApplicationDependency
- NEVER extend another ApplicationDependency implementation
- NEVER skip `base.Method()` call in ApplicationDependency overrides
- NEVER use constructor injection in Commands
- NEVER call lifecycle methods out of order
- NEVER add ApplicationDependency without corresponding ServiceCollectionExtensions
- NEVER make ServiceCollectionExtensions public
- NEVER register DI from another layer (all layers converge in Program.cs)
- NEVER forget `.RunAsync(args)` in Program.cs