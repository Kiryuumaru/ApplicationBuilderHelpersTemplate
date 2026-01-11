using Presentation.WebApi.FunctionalTests.Fixtures;

namespace Presentation.WebApi.FunctionalTests;

/// <summary>
/// Test collection for WebApi functional tests.
/// Tests run in parallel with isolated hosts (random ports, separate databases).
/// Each test class inherits from WebApiTestBase to get its own WebApiTestHost.
/// </summary>
/// <remarks>
/// The collection is kept for organizational purposes and to share Playwright browser
/// if needed, but parallelization is enabled. Each test class manages its own WebApiTestHost.
/// </remarks>
[CollectionDefinition(Name)]
public class WebApiTestCollection
{
    public const string Name = "WebApi Tests";
}
