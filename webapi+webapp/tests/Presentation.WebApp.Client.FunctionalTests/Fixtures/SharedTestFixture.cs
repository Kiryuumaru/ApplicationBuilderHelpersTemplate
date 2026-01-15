using System.Diagnostics;
using Microsoft.Playwright;

namespace Presentation.WebApp.Client.FunctionalTests.Fixtures;

/// <summary>
/// Shared test fixture that manages a single WebApp host and browser.
/// Used with xUnit's ICollectionFixture to share across all tests in a collection.
/// Each test gets its own browser context for isolation.
/// 
/// Note: In the unified Blazor Web App architecture, Presentation.WebApp serves both
/// the API endpoints and the Blazor WASM client. There's no separate WebApp static file
/// server needed - the unified app handles everything.
/// </summary>
public sealed class SharedTestFixture : IAsyncLifetime
{
    private WebApiTestHost? _webAppHost;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _initialized;

    /// <summary>
    /// The base URL of the unified WebApp that serves both API and Blazor client.
    /// </summary>
    public string WebApiUrl => _webAppHost?.BaseUrl ?? throw new InvalidOperationException("Not initialized");

    /// <summary>
    /// The base URL for browser navigation. Same as WebApiUrl since the unified app serves both.
    /// </summary>
    public string WebAppUrl => _webAppHost?.BaseUrl ?? throw new InvalidOperationException("Not initialized");
    
    public HttpClient HttpClient => _webAppHost?.HttpClient ?? throw new InvalidOperationException("Not initialized");

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        var sw = Stopwatch.StartNew();
        Console.WriteLine("[FIXTURE] Initializing shared test fixture...");

        // Start the unified WebApp host (serves both API and Blazor client)
        _webAppHost = new WebApiTestHost(new ConsoleLogAdapter());
        await _webAppHost.StartAsync(TimeSpan.FromSeconds(30));
        Console.WriteLine($"[FIXTURE] WebApp started at {_webAppHost.BaseUrl} ({sw.ElapsedMilliseconds}ms)");

        // Install Playwright browsers if needed
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright browser installation failed with exit code {exitCode}");
        }

        // Initialize Playwright
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--disable-gpu", "--no-sandbox", "--disable-dev-shm-usage"]
        });
        Console.WriteLine($"[FIXTURE] Browser launched ({sw.ElapsedMilliseconds}ms)");

        _initialized = true;
        Console.WriteLine($"[FIXTURE] Shared fixture ready in {sw.ElapsedMilliseconds}ms");
    }

    public async Task DisposeAsync()
    {
        Console.WriteLine("[FIXTURE] Disposing shared test fixture...");

        if (_browser != null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_webAppHost != null)
        {
            await _webAppHost.DisposeAsync();
        }

        Console.WriteLine("[FIXTURE] Shared fixture disposed");
    }

    /// <summary>
    /// Creates a new isolated browser context for a test.
    /// Each test should call this to get its own context.
    /// </summary>
    public async Task<IBrowserContext> CreateContextAsync()
    {
        if (_browser == null) throw new InvalidOperationException("Browser not initialized");
        return await _browser.NewContextAsync();
    }

    /// <summary>
    /// Adapter to allow logging to console from fixtures.
    /// </summary>
    private sealed class ConsoleLogAdapter : ITestOutputHelper
    {
        public void WriteLine(string message) => Console.WriteLine(message);
        public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
    }
}
