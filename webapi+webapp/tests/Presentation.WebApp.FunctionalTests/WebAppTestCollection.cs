namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// Test collection for WebApp functional tests.
/// Tests run in parallel - each test class manages its own hosts with random ports.
/// </summary>
[CollectionDefinition(Name)]
public class WebAppTestCollection
{
    public const string Name = "WebApp Integration Tests";
}
