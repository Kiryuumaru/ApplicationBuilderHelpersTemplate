# Agent Operating Notes

This project defines a couple of shared instructions for anyone automating or assisting with the repo. Keep these handy whenever you run commands or need to build/test the solution.

## Terminal Command Safety

- **Do not use pipes (`|`), semicolons (`;`), `&&`, or redirections** in interactive terminal commands—VS Code will treat them as unsafe writes and require manual approval.
- **Run commands separately** instead of chaining; issue distinct terminal calls for `dotnet build`, `dotnet test`, etc.
- **Avoid piping to filters like `Select-String`**. Prefer native flags (`--filter`, `--include`, etc.) or built-in tooling options.
- The above rule has highest precedence. Favor clarity over brevity to maintain auto-approval.

## Build & Run Reference

| Command | Purpose |
|---------|---------|
| `.\build.ps1 init` | Generate `creds.json` if it does not exist. |
| `.\build.ps1 clean` | Remove all build artifacts (`bin`, `obj`, `.vs`, etc.). |
| `.\build.ps1 githubworkflow` | Regenerate GitHub Actions workflow files. |
| `dotnet build` | Build the entire solution. |
| `dotnet test` | Run every test project. |
| `dotnet test tests/Domain.Tests` | Domain unit tests only. |
| `dotnet test tests/Application.Tests` | Application unit tests only. |
| `dotnet test tests/Presentation.WebApp.Tests` | Playwright end-to-end suite. |
| `dotnet run --project src/Presentation.WebApp` | Launch the Blazor web app. |
| `dotnet run --project src/Presentation.Cli` | Run the CLI application. |

### First-Time Setup

1. Execute `.\build.ps1 init` to generate credentials.
2. Run `dotnet build` to restore packages and compile the solution.

### Environment Configuration

Environment definitions live in `src/Domain/AppEnvironment/Constants/AppEnvironments.cs` and follow the `Roles`/`Permissions` pattern:

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

The NUKE build uses `AllValues` when generating `creds.json`, so add new environments there to keep automation in sync.
