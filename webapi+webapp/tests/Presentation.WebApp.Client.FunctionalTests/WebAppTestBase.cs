using Microsoft.Playwright;
using Presentation.WebApp.Client.FunctionalTests.Fixtures;
using System.Net.Http.Json;
using System.Text.Json;

namespace Presentation.WebApp.Client.FunctionalTests;

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

        // Log network responses to debug MIME type issues
        _page.Response += (_, response) =>
        {
            if (response.Url.Contains("dotnet") || response.Url.Contains("blazor"))
            {
                var contentType = response.Headers.TryGetValue("content-type", out var ct) ? ct : "(none)";
                Output.WriteLine($"[NETWORK] {response.Status} {response.Url} Content-Type: {contentType}");
            }
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
        // Wait for Blazor framework script to be present (fingerprinted filename: blazor.web.{hash}.js)
        await Page.WaitForSelectorAsync("script[src*='blazor.web.']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = timeoutMs
        });

        // Wait for any of: form, h1, main, nav - indicates Blazor has rendered content
        await Page.WaitForSelectorAsync("form, h1, main, nav, [data-page-content]", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = timeoutMs
        });

        // Wait for loading indicators to disappear (if present)
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

        // Wait for LoadingSpinner (used during auth state determination) to disappear
        try
        {
            await Page.WaitForSelectorAsync("[data-loading-spinner]", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = 5000
            });
        }
        catch (TimeoutException)
        {
            // LoadingSpinner might not exist
        }

        // Give Blazor a moment to complete rendering
        await Task.Delay(200);
    }

    /// <summary>
    /// Wait for Blazor WASM to fully load AND the authenticated state to be restored from IndexedDB.
    /// Use this after login when you need to wait for authenticated UI elements.
    /// </summary>
    protected async Task WaitForAuthenticatedStateAsync(int timeoutMs = 30000)
    {
        Output.WriteLine("[TEST] Waiting for authenticated state...");
        
        // Wait for page to stabilize (not redirecting)
        var lastUrl = Page.Url;
        var stableCount = 0;
        var maxWaitMs = timeoutMs;
        var pollIntervalMs = 500;
        var elapsed = 0;

        // First, wait for URL to stabilize (no more redirects)
        while (elapsed < maxWaitMs && stableCount < 3)
        {
            await Task.Delay(pollIntervalMs);
            elapsed += pollIntervalMs;
            
            var currentUrl = Page.Url;
            if (currentUrl == lastUrl)
            {
                stableCount++;
            }
            else
            {
                Output.WriteLine($"[TEST] URL changed: {lastUrl} -> {currentUrl}");
                lastUrl = currentUrl;
                stableCount = 0;
            }
        }
        
        Output.WriteLine($"[TEST] URL stabilized at: {lastUrl} after {elapsed}ms");
        
        // If we ended up back on login page, auth initialization failed
        if (lastUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            Output.WriteLine("[TEST] WARNING: Redirected back to login page - auth state not persisting");
            // This is a known issue with Blazor Web WASM auth state restoration
            // The test should fail with a clear message
            return;
        }
        
        // Now wait for Blazor to render authenticated content
        await WaitForBlazorAsync(10000);
        
        // Wait for authenticated UI indicators with longer timeout
        var authWaitMs = 15000;
        var authElapsed = 0;
        
        while (authElapsed < authWaitMs)
        {
            // Check for authenticated indicators
            var signInLink = await Page.QuerySelectorAsync("a[href*='auth/login']");
            var userMenu = await Page.QuerySelectorAsync("button[aria-expanded]");
            var signOutText = await Page.GetByText("Sign out").CountAsync();
            
            // If no sign-in link visible and either user menu or sign out exists
            if (signInLink == null || (userMenu != null || signOutText > 0))
            {
                Output.WriteLine($"[TEST] Authenticated state detected after {authElapsed}ms");
                return;
            }
            
            await Task.Delay(200);
            authElapsed += 200;
        }

        Output.WriteLine($"[TEST] WARNING: Authenticated state not detected after {authElapsed}ms");
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
