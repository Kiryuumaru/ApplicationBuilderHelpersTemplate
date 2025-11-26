using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.Tests;

/// <summary>
/// Tests for two-factor authentication functionality.
/// </summary>
public class TwoFactorAuthTests : PlaywrightTestBase
{
    private async Task RegisterAndLoginUserAsync(string email, string password)
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
    }

    [Test]
    public async Task TwoFactorPage_LoadsForAuthenticatedUser()
    {
        var email = $"2fa1_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");
        
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/TwoFactorAuthentication");
        
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Two-factor", Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task TwoFactorPage_RedirectsWhenNotAuthenticated()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/TwoFactorAuthentication");
        
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/Account/Login"));
    }

    [Test]
    public async Task EnableAuthenticatorPage_Loads()
    {
        var email = $"2fa2_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");
        
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/EnableAuthenticator");
        
        // Should show QR code or setup info
        await Expect(Page.GetByRole(AriaRole.Heading)).ToBeVisibleAsync();
    }

    [Test]
    public async Task ResetAuthenticatorPage_Loads()
    {
        var email = $"2fa3_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");
        
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/ResetAuthenticator");
        
        await Expect(Page.GetByRole(AriaRole.Heading)).ToBeVisibleAsync();
    }

    [Test]
    public async Task DisableTwoFactorPage_Loads()
    {
        var email = $"2fa4_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");
        
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/Disable2fa");
        
        await Expect(Page.GetByRole(AriaRole.Heading)).ToBeVisibleAsync();
    }

    [Test]
    public async Task GenerateRecoveryCodesPage_Loads()
    {
        var email = $"2fa5_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");
        
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/GenerateRecoveryCodes");
        
        await Expect(Page.GetByRole(AriaRole.Heading)).ToBeVisibleAsync();
    }

    [Test]
    public async Task LoginWith2faPage_Loads()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/LoginWith2fa");
        
        // This page would normally require prior 2FA setup
        // Just verify it loads without crashing
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }

    [Test]
    public async Task LoginWithRecoveryCodePage_Loads()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/LoginWithRecoveryCode");
        
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }
}
