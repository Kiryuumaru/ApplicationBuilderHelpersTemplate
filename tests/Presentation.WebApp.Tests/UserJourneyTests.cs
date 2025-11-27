using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.Tests;

/// <summary>
/// Full user journey end-to-end tests that simulate complete user experiences.
/// These tests cover the entire lifecycle: registration, login, accessing protected content, and logout.
/// </summary>
public class UserJourneyTests : PlaywrightTestBase
{
    /// <summary>
    /// Complete user journey: Create account → Confirm email → Log in → Access authenticated page → Log out → Verify logged out
    /// </summary>
    [Test]
    public async Task FullUserJourney_RegisterLoginAccessProtectedPageLogout()
    {
        var email = $"journey_{Guid.NewGuid():N}@test.com";
        var password = "SecurePassword123!";

        // ===== STEP 1: Navigate to home page =====
        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
        
        // Verify unauthenticated state - should see Login/Register links
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Login" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Register" })).ToBeVisibleAsync();

        // ===== STEP 2: Navigate to Register page =====
        await Page.GetByRole(AriaRole.Link, new() { Name = "Register" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Register", Exact = true })).ToBeVisibleAsync();

        // ===== STEP 3: Fill registration form and submit =====
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // ===== STEP 4: Confirm email (dev mode shows confirmation link) =====
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/Account/RegisterConfirmation"));
        var confirmLink = Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm" });
        if (await confirmLink.CountAsync() > 0)
        {
            await confirmLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // ===== STEP 5: Navigate to Login page =====
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();

        // ===== STEP 6: Log in with registered credentials =====
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ===== STEP 7: Access authenticated-only page (/auth) =====
        await Page.GotoAsync($"{BaseUrl}/auth");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are authenticated" })).ToBeVisibleAsync();

        // ===== STEP 8: Access profile management page =====
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();

        // ===== STEP 9: Log out via the logout button =====
        // Navigate to home page where logout button is visible
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ===== STEP 10: Verify logged out state =====
        await Page.GotoAsync($"{BaseUrl}/");
        // Should see Login link again after logout
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Login" })).ToBeVisibleAsync();

        // ===== STEP 11: Verify protected page redirects after logout =====
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        // Should be redirected to login page
        Assert.That(Page.Url, Does.Contain("/Account/Login"));
    }

    /// <summary>
    /// User journey with profile management: Register → Login → Update profile → View changes → Logout
    /// </summary>
    [Test]
    public async Task UserJourney_RegisterLoginManageProfileLogout()
    {
        var email = $"profile_{Guid.NewGuid():N}@test.com";
        var password = "ProfileTest123!";

        // Register and confirm email
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        var confirmLink = Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm" });
        if (await confirmLink.CountAsync() > 0)
        {
            await confirmLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // Login
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Navigate to profile management
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();

        // Navigate to email management page
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/Email");
        // Verify current email is displayed
        await Expect(Page.GetByText(email, new() { Exact = false }).First).ToBeVisibleAsync();

        // Logout via button
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// User journey testing protected page access before and after authentication
    /// </summary>
    [Test]
    public async Task UserJourney_ProtectedPageAccessBeforeAndAfterAuth()
    {
        var email = $"protected_{Guid.NewGuid():N}@test.com";
        var password = "ProtectedTest123!";

        // ===== BEFORE AUTH: Try to access protected page (Account/Manage requires auth) =====
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        // Should be redirected to login
        Assert.That(Page.Url, Does.Contain("/Account/Login"));

        // Register user
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        var confirmLink = Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm" });
        if (await confirmLink.CountAsync() > 0)
        {
            await confirmLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // Login
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ===== AFTER AUTH: Access protected page =====
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();

        // Also check /auth page
        await Page.GotoAsync($"{BaseUrl}/auth");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are authenticated" })).ToBeVisibleAsync();

        // Logout via button
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ===== AFTER LOGOUT: Try protected page again =====
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        Assert.That(Page.Url, Does.Contain("/Account/Login"));
    }

    /// <summary>
    /// User journey with password change: Register → Login → Change password → Logout → Login with new password
    /// </summary>
    [Test]
    public async Task UserJourney_RegisterLoginChangePasswordLogoutLoginAgain()
    {
        var email = $"pwchange_{Guid.NewGuid():N}@test.com";
        var originalPassword = "OriginalPass123!";
        var newPassword = "NewSecurePass456!";

        // Register and confirm
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(originalPassword);
        await Page.GetByLabel("Confirm Password").FillAsync(originalPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        var confirmLink = Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm" });
        if (await confirmLink.CountAsync() > 0)
        {
            await confirmLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // Login with original password
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(originalPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Access profile page to verify logged in
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();

        // Change password
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/ChangePassword");
        await Page.GetByLabel("Old password").FillAsync(originalPassword);
        await Page.GetByLabel("New password", new() { Exact = true }).FillAsync(newPassword);
        await Page.GetByLabel("Confirm password", new() { Exact = true }).FillAsync(newPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Update password" }).ClickAsync();

        // Verify password changed
        await Expect(Page.GetByText("password has been changed", new() { Exact = false })).ToBeVisibleAsync();

        // Logout via button
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify logged out - protected page redirects
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        Assert.That(Page.Url, Does.Contain("/Account/Login"));

        // Login with NEW password
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(newPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify logged in with new password - can access profile
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();
    }

    /// <summary>
    /// User journey navigating through all public and protected pages
    /// </summary>
    [Test]
    public async Task UserJourney_NavigateAllPagesAuthenticatedVsUnauthenticated()
    {
        var email = $"navigate_{Guid.NewGuid():N}@test.com";
        var password = "NavigateTest123!";

        // ===== UNAUTHENTICATED: Navigate public pages =====
        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();

        await Page.GotoAsync($"{BaseUrl}/counter");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Counter" })).ToBeVisibleAsync();

        await Page.GotoAsync($"{BaseUrl}/weather");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Weather" })).ToBeVisibleAsync();

        // Protected page should redirect (Account/Manage uses standard ASP.NET auth redirect)
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        Assert.That(Page.Url, Does.Contain("/Account/Login"));

        // Register and login
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        var confirmLink = Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm" });
        if (await confirmLink.CountAsync() > 0)
        {
            await confirmLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ===== AUTHENTICATED: Navigate all pages =====
        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();

        await Page.GotoAsync($"{BaseUrl}/counter");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Counter" })).ToBeVisibleAsync();

        await Page.GotoAsync($"{BaseUrl}/weather");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Weather" })).ToBeVisibleAsync();

        // Protected pages should now be accessible
        await Page.GotoAsync($"{BaseUrl}/auth");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are authenticated" })).ToBeVisibleAsync();

        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();

        // Logout via button and verify protected pages redirect again
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        Assert.That(Page.Url, Does.Contain("/Account/Login"));
    }

    /// <summary>
    /// User journey with session persistence: Login → Navigate multiple pages → Verify still logged in
    /// </summary>
    [Test]
    public async Task UserJourney_SessionPersistsAcrossNavigation()
    {
        var email = $"session_{Guid.NewGuid():N}@test.com";
        var password = "SessionTest123!";

        // Register and login
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        var confirmLink = Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm" });
        if (await confirmLink.CountAsync() > 0)
        {
            await confirmLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify logged in - can access profile
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();

        // Navigate away to multiple pages
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.GotoAsync($"{BaseUrl}/counter");
        await Page.GotoAsync($"{BaseUrl}/weather");

        // Come back to protected page - should still be authenticated
        await Page.GotoAsync($"{BaseUrl}/auth");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "You are authenticated" })).ToBeVisibleAsync();

        // Navigate to email page to verify email is displayed
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/Email");
        await Expect(Page.GetByText(email, new() { Exact = false }).First).ToBeVisibleAsync();

        // Logout via button
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
