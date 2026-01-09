---
applyTo: '**'
---
# Build Commands Reference

When building, running, or initializing this project, use the following commands:

## NUKE Build Commands

| Command | Description |
|---------|-------------|
| `.\build.ps1 init` | Generate `creds.json` (if not exists) |
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

When setting up a fresh clone:

1. Run `.\build.ps1 init` to generate credentials
2. Run `dotnet build` to build the solution

## Environment Configuration

Environments are configured in `src/Domain/AppEnvironment/Constants/AppEnvironments.cs`, following the same pattern as `Roles.cs` and `Permissions.cs`:

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

The `AllValues` array is used by the build system to generate `creds.json` with the correct environment structure.
