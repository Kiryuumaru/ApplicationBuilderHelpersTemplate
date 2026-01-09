using Presentation.WebApi.FunctionalTests.Fixtures;

namespace Presentation.WebApi.FunctionalTests;

/// <summary>
/// Test collection that disables parallelism.
/// All tests in this collection share a common WebApi host.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class WebApiTestCollection : ICollectionFixture<SharedWebApiHost>
{
    public const string Name = "WebApi Tests";
}

/// <summary>
/// Shared WebApi host for all tests in the collection.
/// Started once and shared across all tests.
/// </summary>
public class SharedWebApiHost : IAsyncLifetime
{
    private WebApiTestHost? _host;
    public WebApiTestHost Host => _host ?? throw new InvalidOperationException("Host not initialized");
    
    // Use a fixed port for the shared host
    private const int SharedPort = 5199;
    
    public async Task InitializeAsync()
    {
        _host = new WebApiTestHost(new ConsoleTestOutputHelper(), SharedPort);
        await _host.StartAsync(TimeSpan.FromSeconds(60));
    }

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.DisposeAsync();
        }
    }
    
    /// <summary>
    /// Console output helper for shared fixture (can't have ITestOutputHelper in fixture constructor).
    /// </summary>
    private class ConsoleTestOutputHelper : ITestOutputHelper
    {
        public void WriteLine(string message) => Console.WriteLine(message);
        public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
    }
}
