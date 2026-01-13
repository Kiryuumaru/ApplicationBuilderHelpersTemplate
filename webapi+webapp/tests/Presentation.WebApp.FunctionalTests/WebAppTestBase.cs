using Microsoft.Playwright;
using Presentation.WebApp.FunctionalTests.Fixtures;
using System.Net.Http.Json;
using System.Text.Json;

namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// Base class for WebApp functional tests with parallel execution support.
/// Uses a shared WebApi + WebApp host via collection fixture for efficiency.
/// Each test gets its own isolated browser context.
/// </summary>
[Collection(WebAppTestCollection.Name)]
public abstract class WebAppTestBase : IAsyncLifetime
{
    /// <summary>
    /// Default timeout for Playwright operations in milliseconds (15 seconds).
    /// </summary>
    protected const int DefaultTimeoutMs = 15_000;

    private readonly SharedTestFixture _fixture;
    private IBrowserContext? _context;
    private IPage? _page;

    /// <summary>
    /// Test output helper for logging.
    /// </summary>
    protected readonly ITestOutputHelper Output;

    /// <summary>
    /// HTTP client for direct API calls (bypassing the browser).
    /// </summary>
    protected HttpClient HttpClient => _fixture.HttpClient;

    /// <summary>
    /// Base URL for the WebApi server.
    /// </summary>
    protected string WebApiUrl => _fixture.WebApiUrl;

    /// <summary>
    /// Base URL for the WebApp server.
    /// </summary>
    protected string WebAppUrl => _fixture.WebAppUrl;

    /// <summary>
    /// The browser context for this test class.
    /// </summary>
    protected IBrowserContext Context => _context ?? throw new InvalidOperationException("Browser context not initialized");

    /// <summary>
    /// The browser page for this test class.
    /// </summary>
    protected IPage Page => _page ?? throw new InvalidOperationException("Page not initialized");

    /// <summary>
    /// JSON serialization options with case-insensitive property names.
    /// </summary>
    protected static JsonSerializerOptions JsonOptions { get; } = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Standard test password used across tests.
    /// </summary>
    protected const string TestPassword = "TestPassword123!";

    protected WebAppTestBase(SharedTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        Output = output;
    }

    public virtual async Task InitializeAsync()
    {
        Output.WriteLine("[TEST] Creating browser context...");

        // Create isolated browser context from shared fixture
        _context = await _fixture.CreateContextAsync();
        _page = await _context.NewPageAsync();

        // Set default timeouts
        _page.SetDefaultTimeout(DefaultTimeoutMs);
        _page.SetDefaultNavigationTimeout(DefaultTimeoutMs);

        // Log browser console messages
        _page.Console += (_, msg) =>
        {
            Output.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");
        };

        // Log page errors
        _page.PageError += (_, error) =>
        {
            Output.WriteLine($"[BROWSER ERROR] {error}");
        };

        Output.WriteLine($"[TEST] Browser context ready. WebApp at {WebAppUrl}");
    }

    public virtual async Task DisposeAsync()
    {
        Output.WriteLine("[TEST] Disposing browser context...");

        if (_page != null)
        {
            await _page.CloseAsync();
        }

        if (_context != null)
        {
            await _context.CloseAsync();
        }

        Output.WriteLine("[TEST] Browser context disposed");
    }

    #region Navigation Helpers

    /// <summary>
    /// Navigate to the WebApp home page.
    /// </summary>
    protected async Task GoToHomeAsync()
    {
        await Page.GotoAsync(WebAppUrl);
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Navigate to the login page.
    /// </summary>
    protected async Task GoToLoginAsync()
    {
        await Page.GotoAsync($"{WebAppUrl}/auth/login");
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Navigate to the register page.
    /// </summary>
    protected async Task GoToRegisterAsync()
    {
        await Page.GotoAsync($"{WebAppUrl}/auth/register");
        await WaitForBlazorAsync();
    }

    /// <summary>
    /// Wait for Blazor WASM to fully load and hydrate.
    /// </summary>
    protected async Task WaitForBlazorAsync(int timeoutMs = 30000)
    {
        // Wait for Blazor to initialize by checking for the app element
        await Page.WaitForSelectorAsync("#app", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = timeoutMs
        });

        // Wait for the loading indicator to disappear (if present)
        try
        {
            await Page.WaitForSelectorAsync(".loading", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = 5000
            });
        }
        catch (TimeoutException)
        {
            // Loading indicator might not exist
        }

        // Give Blazor a moment to hydrate
        await Task.Delay(500);
    }

    #endregion

    #region Authentication Helpers

    /// <summary>
    /// Register a new user account via the WebApp UI.
    /// </summary>
    protected async Task<bool> RegisterUserAsync(string username, string email, string password)
    {
        await GoToRegisterAsync();

        Output.WriteLine($"[TEST] Registering user: {username}");

        // Fill in registration form (using actual IDs from Register.razor)
        await Page.FillAsync("input#username", username);
        await Page.FillAsync("input#email", email);
        await Page.FillAsync("input#password", password);
        await Page.FillAsync("input#confirmPassword", password);

        // Accept terms checkbox
        var termsCheckbox = await Page.QuerySelectorAsync("input#terms");
        if (termsCheckbox != null)
        {
            await termsCheckbox.CheckAsync();
        }

        // Submit the form and wait for the API request to complete
        Output.WriteLine("[TEST] Clicking submit button...");
        await Page.ClickAsync("button[type='submit']");
        
        // Wait for navigation away from register page using polling
        // The HTTP request takes ~1.2s, then Blazor processes and navigates
        Output.WriteLine("[TEST] Waiting for navigation after submit...");
        var success = false;
        var maxWaitMs = 20000; // 20 second max wait
        var pollIntervalMs = 200;
        var elapsed = 0;
        
        while (elapsed < maxWaitMs)
        {
            await Task.Delay(pollIntervalMs);
            elapsed += pollIntervalMs;
            
            var currentUrl = Page.Url;
            if (!currentUrl.Contains("/auth/register", StringComparison.OrdinalIgnoreCase))
            {
                Output.WriteLine($"[TEST] Navigation detected after {elapsed}ms. URL: {currentUrl}");
                success = true;
                break;
            }
        }
        
        if (!success)
        {
            Output.WriteLine($"[TEST] Navigation timeout after {elapsed}ms. Still on: {Page.Url}");
        }

        Output.WriteLine($"[TEST] Registration {(success ? "succeeded" : "failed")}. Current URL: {Page.Url}");

        return success;
    }

    /// <summary>
    /// Login with credentials via the WebApp UI.
    /// </summary>
    protected async Task<bool> LoginAsync(string email, string password)
    {
        await GoToLoginAsync();

        Output.WriteLine($"[TEST] Logging in as: {email}");

        // Fill in login form (page uses email, not username)
        await Page.FillAsync("input#email, input[type='email']", email);
        await Page.FillAsync("input#password, input[type='password']", password);

        // Submit the form
        await Page.ClickAsync("button[type='submit']");

        // Wait for navigation away from login page using polling
        // The HTTP request takes ~500ms, then Blazor processes and navigates
        Output.WriteLine("[TEST] Waiting for navigation after login...");
        var success = false;
        var maxWaitMs = 20000; // 20 second max wait
        var pollIntervalMs = 200;
        var elapsed = 0;

        while (elapsed < maxWaitMs)
        {
            await Task.Delay(pollIntervalMs);
            elapsed += pollIntervalMs;

            var currentUrl = Page.Url;
            if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
            {
                Output.WriteLine($"[TEST] Navigation detected after {elapsed}ms. URL: {currentUrl}");
                success = true;
                break;
            }
        }

        if (!success)
        {
            Output.WriteLine($"[TEST] Login timeout after {elapsed}ms. Still on: {Page.Url}");
        }

        Output.WriteLine($"[TEST] Login {(success ? "succeeded" : "failed")}. Current URL: {Page.Url}");

        return success;
    }

    /// <summary>
    /// Register and login a new user, returning authentication tokens.
    /// </summary>
    protected async Task<(string AccessToken, string RefreshToken)?> RegisterAndLoginViaApiAsync(string? username = null)
    {
        username ??= $"testuser_{Guid.NewGuid():N}";
        var email = $"{username}@example.com";

        // Register via API
        var registerRequest = new
        {
            Username = username,
            Email = email,
            Password = TestPassword,
            ConfirmPassword = TestPassword
        };
        var registerResponse = await HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        if (!registerResponse.IsSuccessStatusCode)
        {
            return null;
        }

        // Login via API
        var loginRequest = new { Username = username, Password = TestPassword };
        var loginResponse = await HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        if (!loginResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await loginResponse.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
        return (authResponse!.AccessToken, authResponse.RefreshToken);
    }

    /// <summary>
    /// Logout via the WebApp UI.
    /// </summary>
    protected async Task LogoutAsync()
    {
        Output.WriteLine("[TEST] Logging out...");

        var logoutButton = await Page.QuerySelectorAsync("button:has-text('Logout'), a:has-text('Logout'), [data-testid='logout']");

        if (logoutButton != null)
        {
            await logoutButton.ClickAsync();
            await Task.Delay(500);
        }
        else
        {
            await Page.GotoAsync($"{WebAppUrl}/auth/logout");
        }

        await WaitForBlazorAsync();

        Output.WriteLine("[TEST] Logout completed");
    }

    /// <summary>
    /// Check if user is currently authenticated by looking for authenticated UI elements.
    /// </summary>
    protected async Task<bool> IsAuthenticatedAsync()
    {
        var logoutButton = await Page.QuerySelectorAsync("button:has-text('Logout'), a:has-text('Logout')");
        var userMenu = await Page.QuerySelectorAsync("[data-testid='user-menu'], .user-menu, .user-profile");

        return logoutButton != null || userMenu != null;
    }

    #endregion

    #region Wait Helpers

    /// <summary>
    /// Wait for an element to contain specific text.
    /// </summary>
    protected async Task WaitForTextAsync(string selector, string text, int timeoutMs = 5000)
    {
        await Page.WaitForFunctionAsync(
            $"() => document.querySelector('{selector}')?.innerText?.includes('{text}')",
            null,
            new PageWaitForFunctionOptions { Timeout = timeoutMs });
    }

    /// <summary>
    /// Wait for URL to contain a specific path.
    /// </summary>
    protected async Task WaitForUrlAsync(string urlPart, int timeoutMs = 5000)
    {
        await Page.WaitForURLAsync($"**/*{urlPart}*", new PageWaitForURLOptions { Timeout = timeoutMs });
    }

    #endregion

    #region Assertion Helpers

    /// <summary>
    /// Assert that an element with the given text exists on the page.
    /// </summary>
    protected async Task AssertTextVisibleAsync(string text)
    {
        var element = await Page.QuerySelectorAsync($"text={text}");
        Assert.NotNull(element);
    }

    /// <summary>
    /// Assert that we're on a specific page path.
    /// </summary>
    protected void AssertUrlContains(string path)
    {
        Assert.Contains(path, Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Assert that we're NOT on a specific page path.
    /// </summary>
    protected void AssertUrlDoesNotContain(string path)
    {
        Assert.DoesNotContain(path, Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region DTOs

    private record AuthResponse(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresIn);

    #endregion
}
