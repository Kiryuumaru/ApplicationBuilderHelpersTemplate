namespace Presentation.WebApp.FunctionalTests.Auth;

/// <summary>
/// Playwright functional tests for user registration flow.
/// Tests the end-to-end registration experience through the Blazor WebApp.
/// </summary>
[Collection(WebAppTestCollection.Name)]
public class RegisterTests : PlaywrightTestBase
{
    private const string TestPassword = "TestPassword123!";

    public RegisterTests(SharedTestHosts sharedHosts, ITestOutputHelper output)
        : base(sharedHosts, output)
    {
    }

    [Fact]
    public async Task Register_NewUser_RedirectsToLoginOrHome()
    {
        // Arrange
        var username = $"reg_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        // Act
        var success = await RegisterUserAsync(username, email, TestPassword);

        // Assert
        Assert.True(success, "Registration should succeed for a new user");
    }

    [Fact]
    public async Task Register_PageLoads_ShowsRegistrationForm()
    {
        // Act
        await GoToRegisterAsync();

        // Assert - Verify form elements exist
        var usernameInput = await Page.QuerySelectorAsync("input[name='username'], input[placeholder*='username' i]");
        var emailInput = await Page.QuerySelectorAsync("input[name='email'], input[type='email']");
        var passwordInput = await Page.QuerySelectorAsync("input[name='password'], input[type='password']");
        var submitButton = await Page.QuerySelectorAsync("button[type='submit']");

        Assert.NotNull(usernameInput);
        Assert.NotNull(emailInput);
        Assert.NotNull(passwordInput);
        Assert.NotNull(submitButton);
    }

    [Fact]
    public async Task Register_HasLinkToLogin()
    {
        // Act
        await GoToRegisterAsync();

        // Assert - Should have a link to login page
        var loginLink = await Page.QuerySelectorAsync("a[href*='login' i]");
        Assert.NotNull(loginLink);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ShowsError()
    {
        // Arrange
        await GoToRegisterAsync();
        var username = $"reg_{Guid.NewGuid():N}".Substring(0, 20);

        // Act - Use a weak password
        await Page.FillAsync("input[name='username'], input[placeholder*='username' i]", username);
        await Page.FillAsync("input[name='email'], input[type='email']", $"{username}@test.example.com");
        await Page.FillAsync("input[name='password'], input[type='password']:first-of-type", "weak");

        var confirmPasswordField = await Page.QuerySelectorAsync("input[name='confirmPassword'], input[type='password']:nth-of-type(2)");
        if (confirmPasswordField != null)
        {
            await confirmPasswordField.FillAsync("weak");
        }

        await Page.ClickAsync("button[type='submit']");
        await Task.Delay(1000);

        // Assert - Should still be on register page (validation failed)
        // Note: Depending on implementation, could also show validation message
        var currentUrl = Page.Url;
        Output.WriteLine($"URL after weak password: {currentUrl}");

        // Either shows error message or stays on register page
        var hasError = await Page.QuerySelectorAsync(".error, .validation-error, [class*='error'], .text-red");
        var stillOnRegister = currentUrl.Contains("/auth/register", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasError != null || stillOnRegister, "Should show error or stay on register page");
    }
}
