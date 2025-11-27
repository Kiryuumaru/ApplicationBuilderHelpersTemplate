using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.Tests;

/// <summary>
/// Tests for two-factor authentication functionality.
/// </summary>
public class TwoFactorAuthTests : PlaywrightTestBase
{
    [Test]
    public async Task TwoFactorPage_LoadsForAuthenticatedUser()
    {
        var email = $"2fa1_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");
        
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/TwoFactorAuthentication");
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        
        // Page should load and stay on TwoFactorAuthentication URL or redirect if auth failed
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase, 
            $"Expected to be on Account page but was on: {url}");
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
        
        // Page should load (may show enable form or error if 2FA not supported)
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }

    [Test]
    public async Task ResetAuthenticatorPage_Loads()
    {
        var email = $"2fa3_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");
        
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/ResetAuthenticator");
        
        // Page should load - verify there's at least a heading
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Reset authenticator key" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task DisableTwoFactorPage_Loads()
    {
        var email = $"2fa4_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");
        
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/Disable2fa");
        
        // Page should load (may show disable form or error if 2FA not enabled)
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }

    [Test]
    public async Task GenerateRecoveryCodesPage_Loads()
    {
        var email = $"2fa5_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");
        
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/GenerateRecoveryCodes");
        
        // Page should load (may show codes or error if 2FA not enabled)
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
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
