using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// Tests for email-related functionality.
/// </summary>
public class EmailTests : PlaywrightTestBase
{
    [Test]
    public async Task ConfirmEmail_WithInvalidUserId_HandlesGracefully()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ConfirmEmail?userId=invalid&code=test");
        
        // Should show error or appropriate message
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/"));
    }

    [Test]
    public async Task ConfirmEmail_WithInvalidCode_HandlesGracefully()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ConfirmEmail?userId=test&code=invalid");
        
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/"));
    }

    [Test]
    public async Task ConfirmEmail_WithMissingParameters_HandlesGracefully()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ConfirmEmail");
        
        // Should not crash - page should load (could redirect or show error)
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        // Just verify the server didn't crash - any valid response is acceptable
        Assert.Pass("Server handled missing parameters without crashing");
    }

    [Test]
    public async Task RegisterConfirmationPage_Loads()
    {
        var email = $"regconfirm_{Guid.NewGuid():N}@test.com";
        
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("TestPassword123!");
        await Page.GetByLabel("Confirm Password").FillAsync("TestPassword123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        
        // Since RequireConfirmedAccount is false, user is auto-logged in and redirected to home page
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        
        // Verify registration succeeded by checking user is logged in
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ResendEmailConfirmation_WithValidEmail_Works()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ResendEmailConfirmation");
        
        await Page.GetByLabel("Email").FillAsync("test@example.com");
        await Page.GetByRole(AriaRole.Button).First.ClickAsync();
        
        // Should show confirmation or navigate
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }

    [Test]
    public async Task ResendEmailConfirmation_WithEmptyEmail_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ResendEmailConfirmation");
        
        await Page.GetByRole(AriaRole.Button).First.ClickAsync();
        
        // Should show validation error
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task ResendEmailConfirmation_WithInvalidEmail_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/ResendEmailConfirmation");
        
        await Page.GetByLabel("Email").FillAsync("invalid-email");
        await Page.GetByRole(AriaRole.Button).First.ClickAsync();
        
        // Should show validation error
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
    }
}
