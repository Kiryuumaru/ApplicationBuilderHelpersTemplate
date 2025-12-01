# ApplicationBuilderHelpersTemplate

A production-ready .NET 10 application template featuring Clean Architecture, Entity Framework Core, and complete ASP.NET Identity integration with Blazor UI.

## 🚀 Quick Start

Run this command to create a new project from this template:

```powershell
C:\Windows\System32\WindowsPowerShell\v1.0\powershell -c "& ([ScriptBlock]::Create((irm https://raw.githubusercontent.com/Kiryuumaru/ApplicationBuilderHelpersTemplate/master/init.ps1)))"
```

## 📋 Overview

This template provides a robust foundation for building enterprise .NET applications with:

- **Clean Architecture (DDD)** - Strict separation of concerns with Domain, Application, Infrastructure, and Presentation layers
- **Entity Framework Core** - Modular database persistence with SQLite (easily swappable to PostgreSQL, SQL Server, etc.)
- **Full Microsoft Identity** - Complete authentication and authorization with custom user/role stores
- **Blazor Server UI** - Modern web interface with all authentication flows built-in
- **Comprehensive Testing** - 254 tests ensuring reliability across all layers

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              PRESENTATION                               │
│                     WebApp (Blazor) │ CLI (Console)                     │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                          INFRASTRUCTURE                           │  │
│  │   EFCore.Sqlite │ EFCore.Identity │ EFCore.LocalStore │ EFCore    │  │
│  │  ┌─────────────────────────────────────────────────────────────┐  │  │
│  │  │                        APPLICATION                          │  │  │
│  │  │   Services │ Authorization │ Identity │ Configuration       │  │  │
│  │  │  ┌───────────────────────────────────────────────────────┐  │  │  │
│  │  │  │                       DOMAIN                          │  │  │  │
│  │  │  │     User │ Role │ AppEnvironment │ Authorization      │  │  │  │
│  │  │  │                   No Dependencies                     │  │  │  │
│  │  │  └───────────────────────────────────────────────────────┘  │  │  │
│  │  └─────────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘

Dependencies flow inward: Outer layers depend on inner layers, never reverse.
```

| Layer | Description |
|-------|-------------|
| **Domain** | Core business entities (`User`, `Role`, `AppEnvironment`) and rules. Has no external dependencies. |
| **Application** | Business logic, services, interfaces, and authorization. Depends only on Domain. |
| **Infrastructure** | Database implementations (EF Core), Identity stores, external services. Implements Application interfaces. |
| **Presentation** | Entry points (Blazor WebApp, CLI). Composes all layers and handles user interaction. |

## ✨ Features

### Identity & Security
- **Full ASP.NET Identity** - Custom `IUserStore` and `IRoleStore` implementations with:
  - Password Hashing & Validation
  - Email Confirmation & Verification
  - Account Lockout & Failure Counting
  - Two-Factor Authentication (2FA) with Authenticator Apps
  - Passkey/WebAuthn Support
  - External Login Providers
  - Security Stamps for Session Invalidation
  - Recovery Codes
- **Pure Domain Entities** - `User` and `Role` are clean POCOs with no framework dependencies
- **Role-Based Authorization** - Built-in role management system
- **Permission-Based Authorization** - Fine-grained permission grants

### User Interface (Blazor)
- **Complete Auth UI** - All standard identity flows:
  - Login / Register / Logout
  - Forgot / Reset Password
  - Email Confirmation & Change
  - Profile Management
  - Password Change
  - Two-Factor Authentication Setup
  - Passkey Management
  - External Login Management
  - Personal Data (Download/Delete)
- **Protected Pages** - Examples of authenticated and authorized content
- **Responsive Design** - Bootstrap-based UI

### Infrastructure
- **SQLite Database** - Lightweight, file-based storage (easily swappable to PostgreSQL, SQL Server, etc.)
- **LocalStore Service** - Key-value storage for app settings and preferences
- **Automatic Migrations** - Database schema created on startup
- **Code Generators** - Automatic generation of Permission, Role, and Environment constants

## 📁 Project Structure

```
├── build/                                  # NUKE build automation
├── src/
│   ├── Domain/                             # Entities, ValueObjects, Business Rules
│   ├── Domain.CodeGenerator/               # Permission, Role, & BuildConstants generators
│   ├── Application/                        # Use Cases, Services, Interfaces
│   ├── Infrastructure.EFCore/              # Base EF Core DbContext
│   ├── Infrastructure.EFCore.Sqlite/       # SQLite provider
│   ├── Infrastructure.EFCore.Identity/     # Identity stores (User, Role)
│   ├── Infrastructure.EFCore.LocalStore/   # Key-value storage
│   ├── Presentation.Cli/                   # Console application
│   └── Presentation.WebApp/                # Blazor Server application
├── tests/
│   ├── Domain.UnitTests/                   # Domain unit tests
│   ├── Application.UnitTests/              # Application unit tests
│   ├── Application.IntegrationTests/       # Integration tests with real infrastructure
│   └── Presentation.FunctionalTests/       # E2E Playwright tests
└── ApplicationBuilderHelpersTemplate.sln
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

254 tests across four test projects:

- **Domain.UnitTests** (38 tests) - Pure domain logic and entity tests
- **Application.UnitTests** (21 tests) - Application service and authorization tests
- **Application.IntegrationTests** (20 tests) - Integration tests with real infrastructure via DI
- **Presentation.FunctionalTests** (175 tests) - E2E Playwright tests including user journeys, security, and accessibility

```powershell
dotnet test                                         # Run all tests
dotnet test tests/Presentation.FunctionalTests      # Run E2E tests
dotnet test --filter "FullyQualifiedName~UserJourney"  # Run filtered
```

## 🔧 Configuration

### Identity Settings
Configure in `appsettings.json`:
- Password requirements
- Lockout settings
- 2FA options
- Cookie settings

### Switching Database Provider
1. Create a new `Infrastructure.EFCore.{Provider}` project
2. Implement the database context inheriting from `EFCoreDbContext`
3. Register in your Presentation layer's DI container

## 📜 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🙏 Acknowledgments

- [ASP.NET Core](https://github.com/dotnet/aspnetcore)
- [Entity Framework Core](https://github.com/dotnet/efcore)
- [NUKE Build](https://nuke.build/)
- [Playwright](https://playwright.dev/)
