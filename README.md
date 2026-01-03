# ProjectOffworlder

A cryptocurrency trading platform with automated bots, paper trading, and real-time market data. Built with .NET 10, Clean Architecture, and comprehensive test coverage.

> **Status:** ✅ Backend MVP Complete (December 25, 2025)

## 📋 Overview

ProjectOffworlder is a full-featured trading platform that supports:

- **Paper Trading** - Test strategies with real market prices and simulated execution
- **Live Trading** - Connect to exchanges with real API credentials
- **Trading Bots** - Automated trading with configurable strategies
- **Real-Time Data** - Live market prices via REST API and SignalR
- **Comprehensive Testing** - 622 tests ensuring reliability

## ✅ Current Status

| Component | Status |
|-----------|--------|
| REST API | ✅ 110 functional tests |
| Authentication | ✅ JWT with RBAC |
| Exchange Accounts | ✅ Paper + Live support |
| Trading Orders | ✅ Market + Limit |
| SignalR Hubs | ✅ Real-time streaming |
| Bot Framework | ✅ Templates + Instances |
| User Management | ✅ Admin + Self-service |
| UI/WebApp | 🔲 Deferred |

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [API Reference](docs/api-reference.md) | Complete REST API documentation |
| [Authentication](docs/authentication.md) | JWT auth and RBAC |
| [Paper Trading](docs/paper-trading.md) | Paper account system |
| [Trading Bots](docs/trading-bots.md) | Bot framework guide |
| [Market Data](docs/market-data.md) | Market data endpoints |
| [Testing](docs/testing.md) | Test architecture |
| [Future Roadmap](docs/roadmap-future.md) | Planned features |

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              PRESENTATION                               │
│                   WebApi (REST + SignalR) │ WebApp (Blazor)             │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                          INFRASTRUCTURE                           │  │
│  │    Binance │ EFCore.Trading │ EFCore.Identity │ EFCore.LocalStore │  │
│  │  ┌─────────────────────────────────────────────────────────────┐  │  │
│  │  │                        APPLICATION                          │  │  │
│  │  │   Trading │ Authorization │ Identity │ Configuration        │  │  │
│  │  │  ┌───────────────────────────────────────────────────────┐  │  │  │
│  │  │  │                       DOMAIN                          │  │  │  │
│  │  │  │   Trading │ Identity │ Authorization │ AppEnvironment │  │  │  │
│  │  │  │                   No Dependencies                     │  │  │  │
│  │  │  └───────────────────────────────────────────────────────┘  │  │  │
│  │  └─────────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘

Dependencies flow inward: Outer layers depend on inner layers, never reverse.
```

| Layer | Description |
|-------|-------------|
| **Domain** | Core trading entities (`Order`, `Trade`, `BotTemplate`, `ExchangeAccount`) and rules. Has no external dependencies. |
| **Application** | Business logic, services, interfaces. Depends only on Domain. Exchange/persistence ignorant. |
| **Infrastructure** | Binance integration, EF Core stores. Implements Application interfaces. |
| **Presentation** | REST API (WebApi), SignalR hubs, Blazor UI. Composes all layers. |

## ✨ Features

### Trading
- **Paper Trading** - Real market prices, simulated execution, no API keys needed
- **Live Trading** - Connect to Binance with API credentials
- **Market & Limit Orders** - Full order lifecycle support
- **Balance Management** - Track wallets across accounts

### Trading Bots
- **Bot Templates** - Reusable strategy configurations
- **Bot Instances** - Running bots with lifecycle management
- **Pluggable Strategies** - Grid trading (more strategies planned)
- **Signal & Trade Tracking** - Full audit trail

### Real-Time Data
- **REST API** - Market data, prices, candles
- **SignalR Hubs** - Live streaming for prices, bot status, notifications

### Security
- **JWT Authentication** - Access + refresh tokens
- **RBAC** - Role-based access with scope templates
- **Permission Resolution** - Fine-grained endpoint authorization

## 📁 Project Structure

```
├── build/                                  # NUKE build automation
├── docs/                                   # Documentation
│   ├── api-reference.md                    # REST API documentation
│   ├── authentication.md                   # Auth & RBAC
│   ├── paper-trading.md                    # Paper trading guide
│   ├── trading-bots.md                     # Bot framework
│   ├── market-data.md                      # Market data
│   └── testing.md                          # Test architecture
├── src/
│   ├── Domain/                             # Entities, ValueObjects, Business Rules
│   ├── Domain.CodeGenerator/               # Code generators
│   ├── Application/                        # Services, Interfaces
│   ├── Infrastructure.Binance/             # Binance exchange integration
│   ├── Infrastructure.EFCore/              # Base EF Core DbContext
│   ├── Infrastructure.EFCore.Trading/      # Trading stores
│   ├── Infrastructure.EFCore.Identity/     # Identity stores
│   ├── Infrastructure.EFCore.LocalStore/   # Key-value storage
│   ├── Presentation.WebApi/                # REST API + SignalR
│   └── Presentation.WebApp/                # Blazor Server (deferred)
├── tests/
│   ├── Domain.UnitTests/                   # 376 domain tests
│   ├── Application.UnitTests/              # 92 application tests
│   ├── Application.IntegrationTests/       # 37 integration tests
│   └── Presentation.WebApi.FunctionalTests/# 110 API tests
└── ProjectOffworlder.sln
```

## ⚙️ Environment Configuration

Environments are configured in `src/Domain/AppEnvironment/Constants/AppEnvironments.cs`. This is the **single source of truth** for all environment-related configuration, following the same pattern as `Roles.cs` and `Permissions.cs`:

```csharp
public static class AppEnvironments
{
    public static AppEnvironment Development { get; } = new()
    {
        Tag = "prerelease",
        Environment = "Development",
        EnvironmentShort = "pre"
    };

    public static AppEnvironment Production { get; } = new()
    {
        Tag = "master",
        Environment = "Production",
        EnvironmentShort = "prod"
    };

    public static AppEnvironment[] AllValues { get; } = [Development, Production];
}
```

| Property | Description |
|----------|-------------|
| `Tag` | Git branch tag (e.g., `prerelease`, `master`) |
| `Environment` | Environment name, also used as property name |
| `EnvironmentShort` | Short identifier (e.g., `pre`, `prod`) |

> **Note:** The **last environment** in `AllValues` is treated as the main/production branch.

Running `.\build.ps1 init` generates `creds.json` with JWT secrets per environment (only if not exists).

## 🔐 Credentials (`creds.json`)

The `creds.json` file contains environment-specific credentials and is **not committed to the repository**. 

Generated with **secure 64-character alphanumeric secrets** per environment:

```json
{
  "prerelease": {
    "jwt": {
      "secret": "<auto-generated>",
      "issuer": "ApplicationBuilderHelpers",
      "audience": "ApplicationBuilderHelpers"
    }
  },
  "master": { ... }
}
```

The file will not be overwritten if it already exists.

## 🛠️ Build & Run

### Prerequisites
- .NET 10 SDK
- PowerShell Core (for build scripts)

### Commands
```powershell
.\build.ps1 init            # Generate creds.json (if not exists)
.\build.ps1 clean           # Clean build artifacts
.\build.ps1 githubworkflow  # Generate GitHub Actions workflow

dotnet build                # Build the solution
dotnet test                 # Run all tests

dotnet run --project src/Presentation.WebApp  # Run web app
dotnet run --project src/Presentation.Cli     # Run CLI app
```

## 🧪 Testing

622 tests across four test projects:

| Project | Tests | Description |
|---------|-------|-------------|
| Domain.UnitTests | 376 | Pure domain logic |
| Application.UnitTests | 92 | Application services |
| Application.IntegrationTests | 37 | Real infrastructure |
| Presentation.WebApi.FunctionalTests | 110 | Full API coverage |

```powershell
dotnet test                                         # Run all tests
dotnet test tests/Presentation.WebApi.FunctionalTests  # Run API tests
```

## 🔧 Configuration

### Switching Database Provider
Replace SQLite with PostgreSQL, SQL Server, etc. by creating a new Infrastructure provider project.

### Exchange Integration
Currently supports Binance. Multi-exchange support (Bybit, Kraken, etc.) is planned.

## 📜 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🙏 Acknowledgments

- [ASP.NET Core](https://github.com/dotnet/aspnetcore)
- [Entity Framework Core](https://github.com/dotnet/efcore)
- [NUKE Build](https://nuke.build/)
- [Playwright](https://playwright.dev/)
