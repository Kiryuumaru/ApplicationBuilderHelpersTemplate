using Presentation.WebApp.FunctionalTests.Fixtures;

namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// Test collection for WebApp functional tests.
/// All test classes in this collection share a single WebApi host, WebApp host, and browser instance.
/// Each test gets its own isolated browser context for test isolation.
/// </summary>
[CollectionDefinition(Name)]
public class WebAppTestCollection : ICollectionFixture<SharedTestFixture>
{
    public const string Name = "WebApp Integration Tests";
}
