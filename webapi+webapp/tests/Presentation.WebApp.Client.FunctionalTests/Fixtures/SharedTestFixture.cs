using System.Diagnostics;
using Microsoft.Playwright;

namespace Presentation.WebApp.Client.FunctionalTests.Fixtures;

/// <summary>
/// Shared test fixture that manages a single WebApi + WebApp host pair and browser.
/// Used with xUnit's ICollectionFixture to share across all tests in a collection.
/// Each test gets its own browser context for isolation.
/// </summary>
public sealed class SharedTestFixture : IAsyncLifetime
{
    private WebApiTestHost? _webApiHost;
    private WebAppTestHost? _webAppHost;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _initialized;

    public string WebApiUrl => _webApiHost?.BaseUrl ?? throw new InvalidOperationException("Not initialized");
    public string WebAppUrl => _webAppHost?.BaseUrl ?? throw new InvalidOperationException("Not initialized");
    public HttpClient HttpClient => _webApiHost?.HttpClient ?? throw new InvalidOperationException("Not initialized");

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        var sw = Stopwatch.StartNew();
        Console.WriteLine("[FIXTURE] Initializing shared test fixture...");

        // Start WebApi host
        _webApiHost = new WebApiTestHost(new ConsoleLogAdapter());
        await _webApiHost.StartAsync(TimeSpan.FromSeconds(30));
        Console.WriteLine($"[FIXTURE] WebApi started at {_webApiHost.BaseUrl} ({sw.ElapsedMilliseconds}ms)");

        // Start WebApp host
        _webAppHost = new WebAppTestHost(new ConsoleLogAdapter(), _webApiHost.BaseUrl);
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

        if (_webApiHost != null)
        {
            await _webApiHost.DisposeAsync();
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
