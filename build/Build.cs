using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Common.Attributes;
using NukeBuildHelpers.Entry;
using NukeBuildHelpers.Entry.Extensions;
using NukeBuildHelpers.Runner.Abstraction;

class Build : BaseNukeBuildHelpers
{
    public static int Main() => Execute<Build>(x => x.Interactive);

    public override string[] EnvironmentBranches { get; } = ["prerelease", "master"];

    public override string MainEnvironmentBranch => "master";

    [SecretVariable("GITHUB_TOKEN")]
    readonly string? GithubToken;

    const string AppId = "sample_app";

    TestEntry TestEntry => _ => _
        .AppId(AppId)
        .Matrix([RunnerOS.Windows2022, RunnerOS.Ubuntu2204], (osTest, osId) => osTest
            .Matrix(["Application.Tests", "Domain.Tests"], (test, testId) => test
                .DisplayName($"Test {testId} on {osId.Name}")
                .WorkflowId($"test_{osId.Name}_{testId}".Replace(".", "_").Replace("-", "_").ToLowerInvariant())
                .RunnerOS(osId)
                .Execute(() =>
                {
                    string projFile = RootDirectory / "tests" / testId / $"{testId}.csproj";
                    DotNetTasks.DotNetClean(_ => _
                        .SetProject(projFile));
                    DotNetTasks.DotNetTest(_ => _
                        .SetProcessAdditionalArguments(
                            "--logger \"GitHubActions;summary.includePassedTests=true;summary.includeSkippedTests=true\" " +
                            "-- " +
                            "RunConfiguration.CollectSourceInformation=true " +
                            "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencovere ")
                        .SetProjectFile(projFile));
                })));

    BuildEntry BuildEntry => _ => _
        .AppId(AppId)
        .RunnerOS(RunnerOS.Windows2022)
        .Execute(() =>
        {
            // build logic here
        });

    PublishEntry PublishEntry => _ => _
        .AppId(AppId)
        .RunnerOS(RunnerOS.Ubuntu2204)
        .Execute(context =>
        {
            // publish logic here
        });
}
