using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.Tests;

/// <summary>
/// Tests for authorization and protected routes.
/// </summary>
public class AuthorizationTests : PlaywrightTestBase
{
    private async Task RegisterAndLoginUserAsync(string email, string password)
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Wait for registration confirmation page and click the email confirmation link
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/Account/RegisterConfirmation"));
        var confirmLink = Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm your account" });
        if (await confirmLink.CountAsync() > 0)
        {
            await confirmLink.ClickAsync();
            await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        }

        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        
        // Wait for login to complete
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
    }

    [Test]
    public async Task ProtectedPage_RedirectsUnauthenticatedUser()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");

        // Should redirect to login with return URL
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/Login"));
    }

    [Test]
    public async Task ProtectedPage_AllowsAuthenticatedUser()
    {
        var email = $"auth1_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");

        await Page.GotoAsync($"{BaseUrl}/Account/Manage");

        // Should not be redirected to login
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/Manage"));
    }

    [Test]
    public async Task LoginPage_HasReturnUrlParameter()
    {
        // Try to access protected page
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/ChangePassword");

        // Should be redirected to login with return URL
        var url = Page.Url;
        Assert.That(url, Does.Contain("ReturnUrl"));
    }

    [Test]
    public async Task PublicPage_AllowsAnonymousAccess()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        // Should load without redirect
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task LoginPage_AllowsAnonymousAccess()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        // Should load without redirect
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegisterPage_AllowsAnonymousAccess()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        // Should load without redirect
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ConfirmEmailPage_AcceptsToken()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ConfirmEmail?userId=test&code=test");

        // Should load (may show error for invalid token, but should load)
        Assert.That(Page.Url, Does.Contain("/Account/ConfirmEmail"));
    }

    [Test]
    public async Task ResetPasswordPage_AcceptsToken()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ResetPassword?code=test");

        // Should load (may show error for invalid token, but should load)
        Assert.That(Page.Url, Does.Contain("/Account/ResetPassword"));
    }

    [Test]
    public async Task AccessDeniedPage_Loads()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/AccessDenied");

        // Should load access denied page
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Access denied", Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task LockoutPage_Loads()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Lockout");

        // Should load lockout page
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Locked out", Exact = false })).ToBeVisibleAsync();
    }
}
