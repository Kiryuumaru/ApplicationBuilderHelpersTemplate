namespace Presentation.WebApp.FunctionalTests.Auth;

/// <summary>
/// Playwright functional tests for user login flow.
/// Tests the end-to-end login experience through the Blazor WebApp.
/// </summary>
[Collection(WebAppTestCollection.Name)]
public class LoginTests : PlaywrightTestBase
{
    private const string TestPassword = "TestPassword123!";

    public LoginTests(SharedTestHosts sharedHosts, ITestOutputHelper output)
        : base(sharedHosts, output)
    {
    }

    [Fact]
    public async Task Login_PageLoads_ShowsLoginForm()
    {
        // Act
        await GoToLoginAsync();

        // Assert - Verify form elements exist
        var usernameInput = await Page.QuerySelectorAsync("input[name='username'], input[placeholder*='username' i]");
        var passwordInput = await Page.QuerySelectorAsync("input[name='password'], input[type='password']");
        var submitButton = await Page.QuerySelectorAsync("button[type='submit']");

        Assert.NotNull(usernameInput);
        Assert.NotNull(passwordInput);
        Assert.NotNull(submitButton);
    }

    [Fact]
    public async Task Login_HasLinkToRegister()
    {
        // Act
        await GoToLoginAsync();

        // Assert - Should have a link to registration page
        var registerLink = await Page.QuerySelectorAsync("a[href*='register' i]");
        Assert.NotNull(registerLink);
    }

    [Fact]
    public async Task Login_WithValidCredentials_RedirectsToHome()
    {
        // Arrange - First register a new user
        var username = $"login_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);

        // Act - Now login
        var success = await LoginAsync(username, TestPassword);

        // Assert
        Assert.True(success, "Login should succeed with valid credentials");
        AssertUrlDoesNotContain("/auth/login");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_StaysOnLoginPage()
    {
        // Arrange - First register a new user
        var username = $"login_bad_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);

        // Act - Try to login with wrong password
        await GoToLoginAsync();
        await Page.FillAsync("input[name='username'], input[placeholder*='username' i]", username);
        await Page.FillAsync("input[name='password'], input[type='password']", "WrongPassword123!");
        await Page.ClickAsync("button[type='submit']");

        await Task.Delay(1000);

        // Assert - Should still be on login page
        var currentUrl = Page.Url;
        Output.WriteLine($"URL after bad password: {currentUrl}");

        var hasError = await Page.QuerySelectorAsync(".error, .validation-error, [class*='error'], .text-red");
        var stillOnLogin = currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasError != null || stillOnLogin, "Should show error or stay on login page");
    }

    [Fact]
    public async Task Login_WithNonExistentUser_StaysOnLoginPage()
    {
        // Arrange
        var username = $"nonexistent_{Guid.NewGuid():N}".Substring(0, 20);

        // Act
        await GoToLoginAsync();
        await Page.FillAsync("input[name='username'], input[placeholder*='username' i]", username);
        await Page.FillAsync("input[name='password'], input[type='password']", TestPassword);
        await Page.ClickAsync("button[type='submit']");

        await Task.Delay(1000);

        // Assert - Should still be on login page
        var currentUrl = Page.Url;
        Output.WriteLine($"URL after non-existent user: {currentUrl}");

        var hasError = await Page.QuerySelectorAsync(".error, .validation-error, [class*='error'], .text-red");
        var stillOnLogin = currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasError != null || stillOnLogin, "Should show error or stay on login page");
    }

    [Fact]
    public async Task Login_ClickRegisterLink_NavigatesToRegister()
    {
        // Arrange
        await GoToLoginAsync();

        // Act - Click register link
        await Page.ClickAsync("a[href*='register' i]");
        await WaitForBlazorAsync();

        // Assert
        AssertUrlContains("/auth/register");
    }
}
