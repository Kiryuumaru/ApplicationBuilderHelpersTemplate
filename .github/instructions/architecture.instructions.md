---
applyTo: '**'
---
# Architecture Rules

## Layer Dependencies

```
┌─────────────────────────────────────────┐
│            PRESENTATION                 │
│  References: Application, Domain        │
│  Infrastructure: Program.cs only        │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│           INFRASTRUCTURE                │
│  References: Application, Domain        │
│  Implements: Application interfaces     │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│            APPLICATION                  │
│  References: Domain only                │
│  Defines: Interfaces, services          │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│              DOMAIN                     │
│  References: Nothing                    │
│  Contains: Entities, value objects      │
└─────────────────────────────────────────┘
```

---

## Composition Root

Infrastructure wiring occurs only in `Program.cs` of executable projects.

```
Presentation.WebApp/Program.cs
Presentation.Cli/Program.cs
```

Controllers, components, services, and commands NEVER import Infrastructure namespaces.

---

## Dependency Direction

- Domain layer MUST reference nothing
- Domain layer MUST NOT reference Application, Infrastructure, or Presentation
- Application layer MAY reference Domain only
- Application layer MUST NOT reference Infrastructure or Presentation
- Infrastructure layer MAY reference Application and Domain
- Infrastructure layer MUST NOT reference Presentation
- Presentation layer MAY reference Application and Domain
- Presentation layer MUST NOT reference Infrastructure except in Program.cs

---

## Abstraction Requirements

- Application services MUST depend on interfaces, not concrete types
- Infrastructure MUST implement Application interfaces
- Presentation MUST inject interfaces via DI
- Domain entities MUST NOT have framework attributes or external dependencies

---

## Service Accessibility

Interfaces: `public`
Implementations: `internal sealed`

```csharp
public interface ILocalStoreService { }
internal sealed class IndexedDBLocalStoreService : ILocalStoreService { }
```

---

## Infrastructure Separation

Providers (SQLite, Postgres) and Features (LocalStore, Identity) are independent.

- Provider projects MAY reference Application and Domain
- Provider projects MUST NOT reference Feature projects
- Feature projects MAY reference Application, Domain, and `IDbContextFactory<T>`
- Feature projects MUST NOT reference Provider projects
- Composition happens at Presentation layer only

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

## Prohibited Patterns

- NEVER use `@inject DbContext` in components
- NEVER use `using Infrastructure.*` outside Program.cs
- NEVER use concrete types in Application constructor parameters
- NEVER use framework attributes (`[Key]`, `[JsonProperty]`) in Domain
- NEVER use static service locator patterns
- NEVER hardcode connection strings or URLs in Application
- NEVER use `if (type == X)` branching in Application layer
