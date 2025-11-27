using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.Tests;

/// <summary>
/// End-to-end tests for complete user flows.
/// </summary>
public class E2EFlowTests : PlaywrightTestBase
{
    [Test]
    public async Task FullRegistrationToLoginFlow()
    {
        var email = $"e2e1_{Guid.NewGuid():N}@test.com";
        var password = "TestPassword123!";

        // Step 1: Go to home page
        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();

        // Step 2: Click register link
        await Page.GetByRole(AriaRole.Link, new() { Name = "Register" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Register", Exact = true })).ToBeVisibleAsync();

        // Step 3: Fill registration form
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Step 3.5: Confirm email using the dev-mode link
        var confirmLink = Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm" });
        if (await confirmLink.CountAsync() > 0)
        {
            await confirmLink.ClickAsync();
        }

        // Step 4: Go to login
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();

        // Step 5: Login with the registered credentials
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        // Step 6: Navigate to profile
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task FullPasswordChangeFlow()
    {
        var email = $"e2e2_{Guid.NewGuid():N}@test.com";
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";

        // Register (user is auto-logged in since RequireConfirmedAccount is false)
        await RegisterAndLoginUserAsync(email, oldPassword);

        // Change password
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/ChangePassword");
        await Page.GetByLabel("Old password").FillAsync(oldPassword);
        await Page.GetByLabel("New password", new() { Exact = true }).FillAsync(newPassword);
        await Page.GetByLabel("Confirm password", new() { Exact = true }).FillAsync(newPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Update password" }).ClickAsync();

        // Verify success
        await Expect(Page.GetByText("password has been changed", new() { Exact = false })).ToBeVisibleAsync();

        // Logout (navigate to home first)
        await Page.GotoAsync($"{BaseUrl}/");
        
        // Try logging in with new password
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(newPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        // Should be able to access profile with new password
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task NavigationFlow_UnauthenticatedUser()
    {
        // Home
        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Login" })).ToBeVisibleAsync();

        // Login page
        await Page.GetByRole(AriaRole.Link, new() { Name = "Login" }).ClickAsync();
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();

        // Register page via link - use exact match to avoid multiple elements
        await Page.GetByRole(AriaRole.Link, new() { Name = "Register as a new user" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Register", Exact = true })).ToBeVisibleAsync();

        // Back to login - use a link that has exact "login" text (could be a link on register page)
        var loginLink = Page.GetByRole(AriaRole.Link, new() { Name = "login", Exact = true });
        if (await loginLink.CountAsync() == 0)
        {
            // Fall back to navigating directly
            await Page.GotoAsync($"{BaseUrl}/Account/Login");
        }
        else
        {
            await loginLink.ClickAsync();
        }
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ForgotPasswordFlow()
    {
        // Go to login
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        // Click forgot password
        await Page.GetByRole(AriaRole.Link, new() { Name = "Forgot", Exact = false }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Forgot", Exact = false })).ToBeVisibleAsync();

        // Submit email
        await Page.GetByLabel("Email").FillAsync("forgottest@example.com");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset" }).ClickAsync();

        // Should show confirmation
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/Account/ForgotPasswordConfirmation"));
    }

    [Test]
    public async Task MultipleUsersCanRegisterAndLogin()
    {
        var users = new[]
        {
            ($"multi1_{Guid.NewGuid():N}@test.com", "Password1!"),
            ($"multi2_{Guid.NewGuid():N}@test.com", "Password2!"),
            ($"multi3_{Guid.NewGuid():N}@test.com", "Password3!")
        };

        // Register all users and confirm their email
        foreach (var (email, password) in users)
        {
            await Page.GotoAsync($"{BaseUrl}/Account/Register");
            await Page.GetByLabel("Email").FillAsync(email);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
            await Page.GetByLabel("Confirm Password").FillAsync(password);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
            
            // Click the confirmation link shown on the page (dev mode only)
            var confirmLink = Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm" });
            if (await confirmLink.CountAsync() > 0)
            {
                await confirmLink.ClickAsync();
            }
        }

        // Login each user and verify
        foreach (var (email, password) in users)
        {
            await Page.GotoAsync($"{BaseUrl}/Account/Login");
            await Page.GetByLabel("Email").FillAsync(email);
            await Page.GetByLabel("Password").FillAsync(password);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

            await Page.GotoAsync($"{BaseUrl}/Account/Manage/Email");
            await Expect(Page.GetByText(email, new() { Exact = false })).ToBeVisibleAsync();
            
            // Logout for next user
            await Page.GotoAsync($"{BaseUrl}/Account/Logout");
        }
    }
}
