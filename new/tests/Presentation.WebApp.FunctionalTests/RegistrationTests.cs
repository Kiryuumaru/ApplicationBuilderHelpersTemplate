using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// Tests for user registration functionality.
/// </summary>
public class RegistrationTests : PlaywrightTestBase
{
    [Test]
    public async Task RegisterPage_Loads()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Register", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Register_WithValidData_CreatesAccount()
    {
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var password = "Test123!@#";

        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Since RequireConfirmedAccount is false, user is auto-logged in and redirected to home page
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        
        // Verify user is logged in by checking they can access the manage page
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Register_WithMismatchedPasswords_ShowsError()
    {
        var email = $"test_{Guid.NewGuid():N}@example.com";

        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("Test123!@#");
        await Page.GetByLabel("Confirm Password").FillAsync("DifferentPassword123!");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Should show password mismatch error in the validation summary
        await Expect(Page.Locator(".text-danger, .validation-message").Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex("password.*do not match", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Register_WithWeakPassword_ShowsError()
    {
        var email = $"test_{Guid.NewGuid():N}@example.com";
        var weakPassword = "123"; // Too short

        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(weakPassword);
        await Page.GetByLabel("Confirm Password").FillAsync(weakPassword);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Should show password requirements error
        await Expect(Page.Locator(".text-danger, .validation-message").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Register_WithInvalidEmail_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        await Page.GetByLabel("Email").FillAsync("invalid-email");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("Test123!@#");
        await Page.GetByLabel("Confirm Password").FillAsync("Test123!@#");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Should show email validation error
        await Expect(Page.Locator(".text-danger, .validation-message").Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex("email", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Register_WithEmptyFields_ShowsValidationErrors()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        // Click register without filling anything
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Should show required field errors
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Register_HasLinkToLogin()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        var loginLink = Page.GetByRole(AriaRole.Link, new() { Name = "login", Exact = false });
        await Expect(loginLink).ToBeVisibleAsync();
    }
}
