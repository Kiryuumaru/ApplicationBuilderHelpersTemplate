using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.Tests;

/// <summary>
/// Tests for user login functionality.
/// </summary>
public class LoginTests : PlaywrightTestBase
{
    [Test]
    public async Task LoginPage_HasExpectedElements()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Password")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_WithInvalidCredentials_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        await Page.GetByLabel("Email").FillAsync("nonexistent@example.com");
        await Page.GetByLabel("Password").FillAsync("WrongPassword123!");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        // Should show invalid login attempt error
        await Expect(Page.GetByText("Invalid", new() { Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_WithEmptyFields_ShowsValidationErrors()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        // Click login without filling anything
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        // Should show required field errors
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_HasLinkToRegister()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var registerLink = Page.GetByRole(AriaRole.Link, new() { Name = "Register as a new user" });
        await Expect(registerLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_HasForgotPasswordLink()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var forgotPasswordLink = Page.GetByRole(AriaRole.Link, new() { Name = "Forgot", Exact = false });
        await Expect(forgotPasswordLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_HasRememberMeOption()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var rememberMe = Page.GetByLabel("Remember me");
        await Expect(rememberMe).ToBeVisibleAsync();
    }

    [Test]
    public async Task ForgotPasswordPage_Loads()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ForgotPassword");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Forgot", Exact = false })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ForgotPassword_WithValidEmail_ShowsConfirmation()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ForgotPassword");

        await Page.GetByLabel("Email").FillAsync("test@example.com");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset" }).ClickAsync();

        // Should redirect to confirmation page
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/Account/ForgotPasswordConfirmation"));
    }

    [Test]
    public async Task ResendEmailConfirmationPage_Loads()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ResendEmailConfirmation");

        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }
}
