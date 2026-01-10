using Microsoft.Playwright;

namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// Base class for Playwright tests with common utilities.
/// Default test timeout is 60 seconds for all Playwright operations.
/// </summary>
public abstract class PlaywrightTestBase : IAsyncLifetime
{
    /// <summary>
    /// Default timeout for Playwright operations in milliseconds (60 seconds).
    /// </summary>
    protected const int DefaultTimeoutMs = 60_000;

    protected readonly SharedTestHosts SharedHosts;
    protected readonly ITestOutputHelper Output;

    private IBrowserContext? _context;
    private IPage? _page;

    protected IBrowserContext Context => _context ?? throw new InvalidOperationException("Context not initialized");
    protected IPage Page => _page ?? throw new InvalidOperationException("Page not initialized");
    protected string WebApiUrl => SharedHosts.WebApiUrl;
    protected string WebAppUrl => SharedHosts.WebAppUrl;

    protected PlaywrightTestBase(SharedTestHosts sharedHosts, ITestOutputHelper output)
    {
        SharedHosts = sharedHosts;
        Output = output;
    }

    public async Task InitializeAsync()
    {
        Output.WriteLine($"[TEST] Creating browser context...");

        _context = await SharedHosts.Playwright.CreateContextAsync();
        _page = await _context.NewPageAsync();

        // Set default timeout for all Playwright operations
        _page.SetDefaultTimeout(DefaultTimeoutMs);
        _page.SetDefaultNavigationTimeout(DefaultTimeoutMs);

        // Log console messages from the page
        _page.Console += (_, msg) =>
        {
            Output.WriteLine($"[BROWSER] {msg.Type}: {msg.Text}");
        };

        // Log page errors
        _page.PageError += (_, error) =>
        {
            Output.WriteLine($"[BROWSER ERROR] {error}");
        };

        Output.WriteLine($"[TEST] Browser context created with {DefaultTimeoutMs}ms timeout");
    }

    public async Task DisposeAsync()
    {
        Output.WriteLine($"[TEST] Disposing browser context...");

        if (_page != null)
        {
            await _page.CloseAsync();
        }

        if (_context != null)
        {
            await _context.CloseAsync();
        }

        Output.WriteLine($"[TEST] Browser context disposed");
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

        // Fill in registration form
        await Page.FillAsync("input[name='username'], input[placeholder*='username' i]", username);
        await Page.FillAsync("input[name='email'], input[type='email']", email);
        await Page.FillAsync("input[name='password'], input[type='password']:first-of-type", password);

        // Look for confirm password field
        var confirmPasswordField = await Page.QuerySelectorAsync("input[name='confirmPassword'], input[type='password']:nth-of-type(2)");
        if (confirmPasswordField != null)
        {
            await confirmPasswordField.FillAsync(password);
        }

        // Submit the form
        await Page.ClickAsync("button[type='submit']");

        // Wait for navigation or response
        await Task.Delay(1000);

        // Check if we're redirected to login or home (success) or still on register (failure)
        var currentUrl = Page.Url;
        var success = !currentUrl.Contains("/auth/register", StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"[TEST] Registration {(success ? "succeeded" : "failed")}. Current URL: {currentUrl}");

        return success;
    }

    /// <summary>
    /// Login with credentials via the WebApp UI.
    /// </summary>
    protected async Task<bool> LoginAsync(string username, string password)
    {
        await GoToLoginAsync();

        Output.WriteLine($"[TEST] Logging in as: {username}");

        // Fill in login form
        await Page.FillAsync("input[name='username'], input[placeholder*='username' i]", username);
        await Page.FillAsync("input[name='password'], input[type='password']", password);

        // Submit the form
        await Page.ClickAsync("button[type='submit']");

        // Wait for navigation or response
        await Task.Delay(1000);

        // Check if we're redirected away from login (success)
        var currentUrl = Page.Url;
        var success = !currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"[TEST] Login {(success ? "succeeded" : "failed")}. Current URL: {currentUrl}");

        return success;
    }

    /// <summary>
    /// Logout via the WebApp UI.
    /// </summary>
    protected async Task LogoutAsync()
    {
        Output.WriteLine("[TEST] Logging out...");

        // Look for logout button or link
        var logoutButton = await Page.QuerySelectorAsync("button:has-text('Logout'), a:has-text('Logout'), [data-testid='logout']");

        if (logoutButton != null)
        {
            await logoutButton.ClickAsync();
            await Task.Delay(500);
        }
        else
        {
            // Navigate to logout URL if button not found
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
        // Look for common authenticated indicators
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
}
