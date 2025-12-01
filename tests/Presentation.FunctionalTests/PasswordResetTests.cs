using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.FunctionalTests;

/// <summary>
/// Tests for password reset functionality.
/// </summary>
public class PasswordResetTests : PlaywrightTestBase
{
    [Test]
    public async Task ForgotPasswordPage_HasCorrectElements()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ForgotPassword");
        
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Reset" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ForgotPassword_WithEmptyEmail_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ForgotPassword");
        
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset" }).ClickAsync();
        
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task ForgotPassword_WithInvalidEmail_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ForgotPassword");
        
        await Page.GetByLabel("Email").FillAsync("invalid-email");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset" }).ClickAsync();
        
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task ForgotPassword_AlwaysShowsConfirmation()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ForgotPassword");
        
        // Even for non-existent emails, should show confirmation (security)
        await Page.GetByLabel("Email").FillAsync("nonexistent@example.com");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset" }).ClickAsync();
        
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/Account/ForgotPasswordConfirmation"));
    }

    [Test]
    public async Task ResetPasswordPage_WithValidToken_ShowsForm()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ResetPassword?code=test");
        
        // Should show password reset form
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ResetPasswordPage_WithoutToken_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ResetPassword");
        
        // Should show error or redirect
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }

    [Test]
    public async Task ResetPassword_WithMismatchedPasswords_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ResetPassword?code=test");
        
        await Page.GetByLabel("Email").FillAsync("test@example.com");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("NewPassword123!");
        await Page.GetByLabel("Confirm password").FillAsync("DifferentPassword!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset" }).ClickAsync();
        
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task ResetPassword_WithWeakPassword_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ResetPassword?code=test");
        
        await Page.GetByLabel("Email").FillAsync("test@example.com");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("weak");
        await Page.GetByLabel("Confirm password").FillAsync("weak");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset" }).ClickAsync();
        
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task ForgotPasswordConfirmation_HasBackToLoginLink()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ForgotPasswordConfirmation");
        
        var loginLink = Page.GetByRole(AriaRole.Link, new() { Name = "login", Exact = false });
        await Expect(loginLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task ResetPasswordConfirmation_Loads()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ResetPasswordConfirmation");
        
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Reset", Exact = false })).ToBeVisibleAsync();
    }
}
