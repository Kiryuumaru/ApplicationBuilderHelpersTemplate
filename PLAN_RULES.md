# Plan: ApplicationBuilderHelpers Rules Update

This document outlines rules to add to `.github/instructions/` based on ApplicationBuilderHelpers framework patterns.

---

## Target Files

| File | Action |
|------|--------|
| `architecture.instructions.md` | Add ApplicationDependency, ServiceCollectionExtensions, ConfigurationExtensions, Serialization, Command sections |
| `file-structure.instructions.md` | Add file placement for all new types, update folder structure diagrams |

---

## Rules for architecture.instructions.md

### ApplicationDependency

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

### ApplicationDependency Lifecycle

Lifecycle methods execute in order:

1. `CommandPreparation(ApplicationBuilder)` — Before command argument parsing
2. `BuilderPreparation(ApplicationHostBuilder)` — After argument parsing, before host builder creation
3. `AddConfigurations(ApplicationHostBuilder, IConfiguration)` — After builder creation
4. `AddServices(ApplicationHostBuilder, IServiceCollection)` — After configuration
5. `AddMiddlewares(ApplicationHost, IHost)` — After DI registration
6. `AddMappings(ApplicationHost, IHost)` — After middleware
7. `RunPreparation(ApplicationHost)` — After mappings (parallel across layers)
8. `RunPreparationAsync(ApplicationHost, CancellationToken)` — After mappings (parallel across layers)

Method usage:
- `CommandPreparation` — Add custom type parsers
- `BuilderPreparation` — Prepare host builder
- `AddConfigurations` — Add configuration providers, bind IOptions
- `AddServices` — Register DI via ServiceCollectionExtensions only
- `AddMiddlewares` — Configure middleware pipeline
- `AddMappings` — Map endpoints, routes, SignalR hubs
- `RunPreparation` — Synchronous initialization/bootstrap
- `RunPreparationAsync` — Asynchronous initialization (database setup, etc.)

---

### ServiceCollectionExtensions

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

### ConfigurationExtensions

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

### Serialization

Each layer MAY have a `Serialization/` folder for source-generated JSON contexts (Native AOT support).

JsonContext rules:
- MUST be `partial class` extending `JsonSerializerContext`
- MUST use `[JsonSourceGenerationOptions]` with `GenerationMode = Metadata`
- MUST use `[JsonSerializable(typeof(T))]` for each type to serialize
- MUST be named `{Layer}JsonContext`
- MUST be `public` if shared across layers (Domain, Application)
- MUST be `internal` if layer-internal only (Infrastructure)

Converters:
- MUST be in `Serialization/Converters/` subfolder
- MUST be named `{Name}JsonConverter`
- MUST extend `JsonConverter<T>`

| Layer | Class Name | Accessibility |
|-------|------------|---------------|
| Domain | `DomainJsonContext` | `public` |
| Application | `ApplicationJsonContext` | `public` |
| Application.Server | `ApplicationServerJsonContext` | `public` |
| Application.Client | `ApplicationClientJsonContext` | `public` |
| Infrastructure.{Name} | `{Name}JsonContext` | `internal` |

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

### Presentation Commands

Every Presentation leaf project MUST have:
- `Program.cs` at project root
- At least one Command in `Commands/` folder

---

### BuildConstantsGenerator

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
- `Build.Constants` — Static build constants
- `Build.ApplicationConstants` — `IApplicationConstants` implementation
- `Build.BaseCommand<T>` — Extends `BaseCommandType` with `ApplicationConstants` wired

`IApplicationConstants` is registered in DI by `BaseCommand.AddServices()`. Inject anywhere via constructor or `@inject`.

---

### Command Hierarchy

```
Presentation.Commands.BaseCommand<T>           ← Manual (shared)
         ↑
Presentation.WebApp.Commands.BaseWebAppCommand<T>  ← Manual (optional intermediate)
         ↑
Build.BaseCommand<T>                           ← Generated per-project
         ↑
MainCommand                                    ← Manual (leaf command)
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

### Command Attributes

`[Command]` — Applied to classes to define command metadata.

| Property | Type | Description |
|----------|------|-------------|
| `Term` | `string?` | Command name (e.g., `"migrate"`, `"serve"`) |
| `Description` | `string?` | Help text for the command |

Constructors:
- `[Command("Description only")]` — Default/main command
- `[Command("name", description: "Description")]` — Named subcommand

---

`[CommandOption]` — Applied to properties to define CLI options (flags).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Term` | `string?` | — | Long option name (`--log-level`) |
| `ShortTerm` | `char?` | — | Short option name (`-l`) |
| `EnvironmentVariable` | `string?` | — | Env var to read from |
| `Required` | `bool` | `false` | Whether required |
| `Description` | `string?` | — | Help text |
| `FromAmong` | `object[]` | `[]` | Allowed values |
| `CaseSensitive` | `bool` | `false` | Case sensitivity for allowed values |

Required can be specified via:
- C# `required` keyword on property
- `Required = true` in attribute

---

`[CommandArgument]` — Applied to properties to define positional arguments (less commonly used).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Name` | `string?` | — | Argument name |
| `Description` | `string?` | — | Help text |
| `Position` | `int` | `0` | Positional order |
| `Required` | `bool` | `false` | Whether required |
| `FromAmong` | `object[]` | `[]` | Allowed values |
| `CaseSensitive` | `bool` | `false` | Case sensitivity for allowed values |

Required can be specified via:
- C# `required` keyword on property
- `Required = true` in attribute

```csharp
[CommandOption('l', "log-level", EnvironmentVariable = "LOG_LEVEL", Description = "Level of logs to show.")]
public LogLevel LogLevel { get; set; } = LogLevel.Information;

[CommandOption("credentials-override", EnvironmentVariable = "CREDENTIALS_OVERRIDE")]
public string? CredentialsOverrideBase64 { get; set; }

[CommandOption("api-key", Description = "API key for authentication.")]
public required string ApiKey { get; set; }
```

---

### Program.cs Structure

Program.cs MUST:
- Create ApplicationBuilder using `ApplicationBuilder.Create()`
- Add ApplicationDependency implementations via `.AddApplication<T>()`
- Add Commands via `.AddCommand<T>()`
- Call `.RunAsync(args)`

```csharp
return await ApplicationBuilder.Create()
    .AddApplication<Domain>()                      // Domain layer
    .AddApplication<Application>()                 // Application layer
    .AddApplication<ServerApplication>()           // Application.Server layer
    .AddApplication<IdentityInfrastructure>()      // Infrastructure layers
    .AddApplication<EFCoreInfrastructure>()
    .AddApplication<EFCoreSqliteInfrastructure>()
    .AddCommand<MainCommand>()                     // Commands
    .RunAsync(args);
```

---

### Prohibited Patterns (ApplicationBuilderHelpers)

- NEVER register services directly in ApplicationDependency
- NEVER extend another ApplicationDependency implementation
- NEVER skip `base.Method()` call in ApplicationDependency overrides
- NEVER use constructor injection in Commands
- NEVER call lifecycle methods out of order
- NEVER add ApplicationDependency without corresponding ServiceCollectionExtensions
- NEVER make ServiceCollectionExtensions public
- NEVER register DI from another layer (all layers converge in Program.cs)
- NEVER forget `.RunAsync(args)` in Program.cs

---

## Rules for file-structure.instructions.md

### ApplicationDependency File Placement

| Layer | File | Location |
|-------|------|----------|
| Domain | `Domain.cs` | `Domain/Domain.cs` |
| Application | `Application.cs` | `Application/Application.cs` |
| Application.Server | `ServerApplication.cs` | `Application.Server/ServerApplication.cs` |
| Application.Client | `ClientApplication.cs` | `Application.Client/ClientApplication.cs` |
| Infrastructure.* | `{Name}Infrastructure.cs` | `Infrastructure.{Name}/{Name}Infrastructure.cs` |

---

### ServiceCollectionExtensions File Placement

| Layer | Location |
|-------|----------|
| Domain | `Domain/{Feature}/Extensions/{Feature}ServiceCollectionExtensions.cs` |
| Application | `Application/{Feature}/Extensions/{Feature}ServiceCollectionExtensions.cs` |
| Application.Server | `Application.Server/{Feature}/Extensions/{Feature}ServiceCollectionExtensions.cs` |
| Application.Client | `Application.Client/{Feature}/Extensions/{Feature}ServiceCollectionExtensions.cs` |
| Infrastructure | `Infrastructure.{Name}/Extensions/{Name}ServiceCollectionExtensions.cs` |
| Shared | `{Layer}/Shared/Extensions/SharedServiceCollectionExtensions.cs` |

---

### ConfigurationExtensions File Placement

| Layer | Location |
|-------|----------|
| Domain | `Domain/{Feature}/Extensions/{Feature}ConfigurationExtensions.cs` |
| Application | `Application/{Feature}/Extensions/{Feature}ConfigurationExtensions.cs` |
| Application.Server | `Application.Server/{Feature}/Extensions/{Feature}ConfigurationExtensions.cs` |
| Application.Client | `Application.Client/{Feature}/Extensions/{Feature}ConfigurationExtensions.cs` |
| Infrastructure | `Infrastructure.{Name}/Extensions/{Name}ConfigurationExtensions.cs` |
| Shared | `{Layer}/Shared/Extensions/SharedConfigurationExtensions.cs` |

---

### Serialization File Placement

| Layer | Location |
|-------|----------|
| Domain | `Domain/Serialization/DomainJsonContext.cs` |
| Application | `Application/Serialization/ApplicationJsonContext.cs` |
| Application.Server | `Application.Server/Serialization/ApplicationServerJsonContext.cs` |
| Application.Client | `Application.Client/Serialization/ApplicationClientJsonContext.cs` |
| Infrastructure.{Name} | `Infrastructure.{Name}/Serialization/{Name}JsonContext.cs` |
| Converters | `{Layer}/Serialization/Converters/{Name}JsonConverter.cs` |

---

### Command File Placement

| Presentation | Location |
|--------------|----------|
| Shared BaseCommand | `Presentation/Commands/BaseCommand.cs` |
| WebApp BaseCommand | `Presentation.WebApp/Commands/BaseWebAppCommand.cs` |
| WebApp.Server | `Presentation.WebApp.Server/Commands/{Name}Command.cs` |
| WebApp.Client | `Presentation.WebApp.Client/Commands/{Name}Command.cs` |
| WebApi | `Presentation.WebApi/Commands/{Name}Command.cs` |
| Cli | `Presentation.Cli/Commands/{Name}Command.cs` |

---

### Updated Folder Structure (add to existing)

```
Domain/
├── Domain.cs                               ← ApplicationDependency
├── Serialization/
│   ├── DomainJsonContext.cs
│   └── Converters/
├── Shared/
│   └── Extensions/
│       └── SharedServiceCollectionExtensions.cs
└── {Feature}/
    ├── Extensions/
    │   └── {Feature}ServiceCollectionExtensions.cs
    ├── Services/
    ├── Entities/
    └── ValueObjects/

Application/
├── Application.cs                          ← ApplicationDependency
├── Serialization/
│   └── ApplicationJsonContext.cs
├── Shared/
│   └── Extensions/
│       └── SharedServiceCollectionExtensions.cs
└── {Feature}/
    ├── Extensions/
    │   └── {Feature}ServiceCollectionExtensions.cs
    └── ...

Application.Server/
├── ServerApplication.cs                    ← ApplicationDependency
├── Serialization/
│   └── ApplicationServerJsonContext.cs
└── {Feature}/
    └── Extensions/
        └── {Feature}ServiceCollectionExtensions.cs

Application.Client/
├── ClientApplication.cs                    ← ApplicationDependency
├── Serialization/
│   └── ApplicationClientJsonContext.cs
└── {Feature}/
    └── Extensions/
        └── {Feature}ServiceCollectionExtensions.cs

Infrastructure.{Name}/
├── {Name}Infrastructure.cs                 ← ApplicationDependency
├── Serialization/
│   └── {Name}JsonContext.cs
└── Extensions/
    └── {Name}ServiceCollectionExtensions.cs

Presentation.WebApp.Server/
├── Program.cs                              ← Entry point
└── Commands/
    └── MainCommand.cs

Presentation.WebApp.Client/
├── Program.cs                              ← Entry point
└── Commands/
    └── MainCommand.cs
```

---

### Type Placement by Kind (add to existing table)

| Type Kind | Required Folder | Example |
|-----------|-----------------|---------|
| ApplicationDependency | Project root | `Application.cs` |
| ServiceCollectionExtensions | `Extensions/` | `Extensions/LocalStoreServiceCollectionExtensions.cs` |
| ConfigurationExtensions | `Extensions/` | `Extensions/EFCoreSqliteConfigurationExtensions.cs` |
| JsonContext | `Serialization/` | `Serialization/DomainJsonContext.cs` |
| JsonConverter | `Serialization/Converters/` | `Serialization/Converters/CamelCaseEnumConverter.cs` |
| Command | `Commands/` | `Commands/MainCommand.cs` |

---

## Implementation Checklist

| # | Task | Target File |
|---|------|-------------|
| 1 | Add ApplicationDependency section | architecture.instructions.md |
| 2 | Add ApplicationDependency lifecycle section | architecture.instructions.md |
| 3 | Add ServiceCollectionExtensions section | architecture.instructions.md |
| 4 | Add ConfigurationExtensions section | architecture.instructions.md |
| 5 | Add Serialization section | architecture.instructions.md |
| 6 | Add BuildConstantsGenerator section | architecture.instructions.md |
| 7 | Add Command Hierarchy section | architecture.instructions.md |
| 8 | Add Program.cs structure section | architecture.instructions.md |
| 9 | Add prohibited patterns | architecture.instructions.md |
| 10 | Add ApplicationDependency file placement | file-structure.instructions.md |
| 11 | Add ServiceCollectionExtensions file placement | file-structure.instructions.md |
| 12 | Add ConfigurationExtensions file placement | file-structure.instructions.md |
| 13 | Add Serialization file placement | file-structure.instructions.md |
| 14 | Add Command file placement | file-structure.instructions.md |
| 15 | Update folder structure diagrams | file-structure.instructions.md |
| 16 | Add type placement entries | file-structure.instructions.md |

---

## Notes

- All patterns derived from actual codebase analysis
- ApplicationBuilderHelpers is the core framework for application composition
- APPLICATION_BUILDER_DOCS.md is the authoritative reference for the framework
- These rules cross-reference with existing architecture rules
