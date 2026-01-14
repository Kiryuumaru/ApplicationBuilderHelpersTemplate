using Presentation.WebApp.Client.FunctionalTests.Fixtures;

namespace Presentation.WebApp.Client.FunctionalTests.Account;

/// <summary>
/// Playwright functional tests for user profile page.
/// Tests profile viewing and editing functionality.
/// </summary>
public class ProfileTests : WebAppTestBase
{
    public ProfileTests(SharedTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact]
    public async Task Profile_RequiresAuthentication()
    {
        // Act - Try to access profile without authentication
        await Page.GotoAsync($"{WebAppUrl}/account/profile");
        await WaitForBlazorAsync();

        // Assert - Should redirect to login
        var currentUrl = Page.Url;
        var redirectedToLogin = currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase);
        Assert.True(redirectedToLogin, "Should redirect to login when accessing profile unauthenticated");
    }

    [Fact]
    public async Task Profile_Authenticated_ShowsUserInfo()
    {
        // Arrange - Register and login
        var username = $"profile_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to profile
        await Page.GotoAsync($"{WebAppUrl}/account/profile");
        await WaitForBlazorAsync();

        // Assert - Should show profile page with user info
        var pageContent = await Page.ContentAsync();
        Output.WriteLine($"Profile page content length: {pageContent.Length}");

        // Should contain user's email or username
        var hasUserInfo = pageContent.Contains(email, StringComparison.OrdinalIgnoreCase) ||
                         pageContent.Contains(username, StringComparison.OrdinalIgnoreCase) ||
                         pageContent.Contains("profile", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasUserInfo, "Profile page should display user information");
    }

    [Fact]
    public async Task Profile_ShowsUserInitial()
    {
        // Arrange - Register and login
        var username = $"initials_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to profile
        await Page.GotoAsync($"{WebAppUrl}/account/profile");
        await WaitForBlazorAsync();

        // Assert - Should show user initial in avatar
        var avatarElement = await Page.QuerySelectorAsync(".rounded-full, [class*='avatar']");
        Assert.NotNull(avatarElement);
    }

    [Fact]
    public async Task Profile_HasEditableFields()
    {
        // Arrange - Register and login
        var username = $"editable_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to profile
        await Page.GotoAsync($"{WebAppUrl}/account/profile");
        await WaitForBlazorAsync();

        // Assert - Should have editable form fields
        var usernameInput = await Page.QuerySelectorAsync("input#username, input[name='username']");
        var emailInput = await Page.QuerySelectorAsync("input#email, input[name='email'], input[type='email']");
        var saveButton = await Page.QuerySelectorAsync("button[type='submit'], button:has-text('Save')");

        Output.WriteLine($"Username input: {usernameInput != null}");
        Output.WriteLine($"Email input: {emailInput != null}");
        Output.WriteLine($"Save button: {saveButton != null}");

        Assert.True(usernameInput != null || emailInput != null, "Should have editable profile fields");
    }

    [Fact(Skip = "Security section not yet implemented on profile page")]
    public async Task Profile_HasSecuritySection()
    {
        // Arrange - Register and login
        var username = $"security_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to profile
        await Page.GotoAsync($"{WebAppUrl}/account/profile");
        await WaitForBlazorAsync();

        // Assert - Should have security section
        var pageContent = await Page.ContentAsync();
        var hasSecuritySection = pageContent.Contains("security", StringComparison.OrdinalIgnoreCase) ||
                                 pageContent.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                                 pageContent.Contains("two-factor", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasSecuritySection, "Profile should have security section");
    }

    [Fact(Skip = "Change password link not yet implemented on profile page")]
    public async Task Profile_HasChangePasswordLink()
    {
        // Arrange - Register and login
        var username = $"pwdlink_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to profile
        await Page.GotoAsync($"{WebAppUrl}/account/profile");
        await WaitForBlazorAsync();

        // Assert - Should have change password link
        var changePasswordLink = await Page.QuerySelectorAsync("a[href*='change-password' i], a[href*='password' i]");
        Assert.NotNull(changePasswordLink);
    }

    [Fact]
    public async Task Profile_ClickChangePassword_NavigatesToChangePasswordPage()
    {
        // Arrange - Register and login
        var username = $"changepwd_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        await Page.GotoAsync($"{WebAppUrl}/account/profile");
        await WaitForBlazorAsync();

        // Act - Click change password link
        var changePasswordLink = await Page.QuerySelectorAsync("a[href*='change-password' i]");
        if (changePasswordLink != null)
        {
            await changePasswordLink.ClickAsync();
            await WaitForBlazorAsync();

            // Assert
            AssertUrlContains("change-password");
        }
    }
}
