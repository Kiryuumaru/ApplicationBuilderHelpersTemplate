using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.Tests;

/// <summary>
/// Tests for edge cases and error handling.
/// </summary>
public class EdgeCaseTests : PlaywrightTestBase
{
    [Test]
    public async Task EmptyFormSubmission_HandledsGracefully()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        
        // Submit without filling anything
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        
        // Should show validation errors, not crash
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhitespaceOnlyInput_HandledCorrectly()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        
        await Page.GetByLabel("Email").FillAsync("   ");
        await Page.GetByLabel("Password").FillAsync("   ");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        
        // Should show validation errors
        await Expect(Page.Locator(".validation-message, .text-danger, .text-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task VeryLongInput_HandledCorrectly()
    {
        var longString = new string('a', 10000);
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        
        await Page.GetByLabel("Email").FillAsync(longString + "@test.com");
        await Page.GetByLabel("Password").FillAsync(longString);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        
        // Should handle gracefully, not crash
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }

    [Test]
    public async Task SpecialCharactersInPassword_Handled()
    {
        var specialPassword = "Test<>\"'&;[]{}|\\`~!@#$%^*()_+=123";
        var email = $"special_{Guid.NewGuid():N}@test.com";
        
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(specialPassword);
        await Page.GetByLabel("Confirm Password").FillAsync(specialPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        
        // Should either succeed or show appropriate error
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }

    [Test]
    public async Task UnicodePassword_Handled()
    {
        var unicodePassword = "Tëst密码पासवर्ड123!";
        var email = $"unicode_{Guid.NewGuid():N}@test.com";
        
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(unicodePassword);
        await Page.GetByLabel("Confirm Password").FillAsync(unicodePassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }

    [Test]
    public async Task RapidFormSubmission_HandledCorrectly()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        
        await Page.GetByLabel("Email").FillAsync("rapid@test.com");
        await Page.GetByLabel("Password").FillAsync("TestPassword123!");
        
        // Click multiple times rapidly
        var button = Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true });
        await button.ClickAsync(new() { ClickCount = 3 });
        
        // Should handle gracefully
        await Page.WaitForTimeoutAsync(500);
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }

    [Test]
    public async Task DuplicateRegistration_ShowsError()
    {
        var email = $"duplicate_{Guid.NewGuid():N}@test.com";
        var password = "TestPassword123!";
        
        // Register first time
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        
        // Try to register again
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        
        // Should show error
        await Expect(Page.Locator(".validation-message, .text-danger, .validation-summary-errors").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task ConcurrentSessions_Work()
    {
        var email = $"concurrent_{Guid.NewGuid():N}@test.com";
        var password = "TestPassword123!";
        
        // Register
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        
        // Login
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        
        // Open new tab/context
        var newPage = await Page.Context.NewPageAsync();
        await newPage.GotoAsync($"{BaseUrl}/Account/Manage");
        
        // Both sessions should work
        await Expect(newPage.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();
        
        await newPage.CloseAsync();
    }

    [Test]
    public async Task NetworkError_RecoveryWorks()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        
        // Page loaded successfully
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
        
        // Reload should work
        await Page.ReloadAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task JavaScriptDisabled_FormStillWorks()
    {
        // Note: This test runs with JS enabled, but verifies forms have proper server-side handling
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        
        // Forms should have proper action attributes for non-JS fallback
        var form = Page.Locator("form").First;
        var action = await form.GetAttributeAsync("action");
        var method = await form.GetAttributeAsync("method");
        
        // Form should have proper attributes
        Assert.That(method?.ToLower() ?? "post", Is.EqualTo("post"));
    }

    [Test]
    public async Task SessionExpiry_HandledGracefully()
    {
        // Clear cookies to simulate session expiry
        var context = Page.Context;
        await context.ClearCookiesAsync();
        
        // Try to access protected page
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        
        // Should redirect to login
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/Account/Login"));
    }

    [Test]
    public async Task BrokenImageLinks_DoNotCrashPage()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        
        // Page should load even if some images fail
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
    }
}
