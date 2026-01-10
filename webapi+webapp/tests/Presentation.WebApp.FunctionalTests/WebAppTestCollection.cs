namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// Test collection that shares WebApi, WebApp hosts and Playwright browser.
/// Tests run in parallel with isolated browser contexts per test.
/// </summary>
[CollectionDefinition(Name)]
public class WebAppTestCollection : ICollectionFixture<SharedTestHosts>
{
    public const string Name = "WebApp Integration Tests";
}
