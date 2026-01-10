using Presentation.WebApp.FunctionalTests.Fixtures;

namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// Shared test hosts fixture that manages WebApi, WebApp, and Playwright browser lifecycle.
/// Started once and shared across all tests in the collection.
/// </summary>
public class SharedTestHosts : IAsyncLifetime
{
    private WebApiTestHost? _webApiHost;
    private WebAppTestHost? _webAppHost;
    private PlaywrightFixture? _playwright;

    // Use fixed ports for the shared hosts
    private const int WebApiPort = 5299;
    private const int WebAppPort = 5298;

    public WebApiTestHost WebApi => _webApiHost ?? throw new InvalidOperationException("WebApi host not initialized");
    public WebAppTestHost WebApp => _webAppHost ?? throw new InvalidOperationException("WebApp host not initialized");
    public PlaywrightFixture Playwright => _playwright ?? throw new InvalidOperationException("Playwright not initialized");

    public string WebApiUrl => WebApi.BaseUrl;
    public string WebAppUrl => WebApp.BaseUrl;

    public async Task InitializeAsync()
    {
        var output = new ConsoleTestOutputHelper();

        Console.WriteLine("[SHARED] Initializing test hosts...");

        // Initialize Playwright first
        _playwright = new PlaywrightFixture();
        await _playwright.InitializeAsync();

        // Start WebApi
        _webApiHost = new WebApiTestHost(output, WebApiPort);
        await _webApiHost.StartAsync(TimeSpan.FromSeconds(60));

        // Start WebApp
        _webAppHost = new WebAppTestHost(output, _webApiHost.BaseUrl, WebAppPort);
        await _webAppHost.StartAsync(TimeSpan.FromSeconds(60));

        Console.WriteLine("[SHARED] All test hosts initialized");
        Console.WriteLine($"[SHARED] WebApi: {_webApiHost.BaseUrl}");
        Console.WriteLine($"[SHARED] WebApp: {_webAppHost.BaseUrl}");
    }

    public async Task DisposeAsync()
    {
        Console.WriteLine("[SHARED] Disposing test hosts...");

        if (_webAppHost != null)
        {
            await _webAppHost.DisposeAsync();
        }

        if (_webApiHost != null)
        {
            await _webApiHost.DisposeAsync();
        }

        if (_playwright != null)
        {
            await _playwright.DisposeAsync();
        }

        Console.WriteLine("[SHARED] All test hosts disposed");
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
