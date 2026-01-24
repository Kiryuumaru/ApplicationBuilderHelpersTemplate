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

## Publishing and Running the Application

### Publish

**Important:** Always run `dotnet build --no-incremental` before publishing. This ensures Tailwind CSS is regenerated and included in the publish output. The `dotnet publish` command alone may use cached build artifacts that don't include recent CSS changes.

```powershell
dotnet build src/Presentation.WebApp.Server --no-incremental
dotnet publish src/Presentation.WebApp.Server -o publish
```

### Run from Publish Folder

**Important:** Always `cd` to the publish directory AND run the executable in a **single combined command**. This ensures:
1. Config files (`appsettings.json`, etc.) are resolved correctly
2. The working directory doesn't reset between commands

**Always use the absolute path to the executable**, even when already in the publish directory.

**Always use `0.0.0.0` (all interfaces) instead of `localhost`** to ensure the app is accessible from browsers and other tools that may not resolve loopback correctly.

```powershell
Push-Location "C:\path\to\publish"; & "C:\path\to\publish\sampleapp.exe" --urls "http://0.0.0.0:5000"
```

| Flag | Description |
|------|-------------|
| `--urls "http://0.0.0.0:5000"` | **Preferred** - Listen on all network interfaces at port 5000 |
| `--urls "http://localhost:5000"` | Listen only on localhost (may cause issues with some tools) |
| `--urls "http://0.0.0.0:80"` | Listen on all interfaces at port 80 (requires admin) |

### Stop a Running Instance

```powershell
Get-Process sampleapp -ErrorAction SilentlyContinue | Stop-Process -Force
```

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
