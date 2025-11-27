---
applyTo: '**'
---
# Build Commands Reference

When building, running, or initializing this project, use the following commands:

## NUKE Build Commands

| Command | Description |
|---------|-------------|
| `.\build.ps1 init` | Generate `creds.json` (if not exists) and `AppEnvironments.Generated.cs` |
| `.\build.ps1 clean` | Clean all build artifacts (bin, obj, .vs folders) |
| `.\build.ps1 githubworkflow` | Generate GitHub Actions workflow files |

## .NET Commands

| Command | Description |
|---------|-------------|
| `dotnet build` | Build the solution |
| `dotnet test` | Run all tests |
| `dotnet test tests/Domain.Tests` | Run Domain unit tests |
| `dotnet test tests/Application.Tests` | Run Application unit tests |
| `dotnet test tests/Presentation.WebApp.Tests` | Run E2E Playwright tests |
| `dotnet run --project src/Presentation.WebApp` | Run the Blazor web application |
| `dotnet run --project src/Presentation.Cli` | Run the CLI application |

## First-Time Setup

When setting up a fresh clone or after modifying `Environments` in `Build.cs`:

1. Run `.\build.ps1 init` to generate credentials and environment constants
2. Run `dotnet build` to build the solution

## Environment Configuration

Environments are configured in `build/Build.cs` using the `AppEnvironment` model:

```csharp
static readonly AppEnvironment[] Environments =
[
    new() { Tag = "prerelease", Environment = "Development", EnvironmentShort = "pre" },
    new() { Tag = "master", Environment = "Production", EnvironmentShort = "prod" }
];
```

After modifying this array, run `.\build.ps1 init` to regenerate `AppEnvironments.Generated.cs`.
