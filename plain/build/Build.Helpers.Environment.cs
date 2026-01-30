using Domain.AppEnvironment.Constants;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;
using System.Linq;

partial class Build
{
    // ─────────────────────────────────────────────────────────────
    // Base Build Overrides
    // ─────────────────────────────────────────────────────────────

    public override string[] EnvironmentBranches =>
        [.. AppEnvironments.AllValues.Select(e => e.Tag)];

    public override string MainEnvironmentBranch =>
        AppEnvironments.AllValues.Last().Tag;

    // ─────────────────────────────────────────────────────────────
    // Targets
    // ─────────────────────────────────────────────────────────────

    Target Clean => _ => _
        .Executes(() =>
        {
            foreach (var path in RootDirectory.GetFiles("**", 99).Where(i => i.Name.EndsWith(".csproj")))
            {
                if (path.Name == "_build.csproj")
                {
                    continue;
                }
                Log.Information("Cleaning {path}", path);
                (path.Parent / "bin").DeleteDirectory();
                (path.Parent / "obj").DeleteDirectory();
            }
            (RootDirectory / ".vs").DeleteDirectory();
        });

    Target Init => _ => _
        .Executes(() =>
        {
            Log.Information("Plain template initialized. No credentials required.");
        });
}
