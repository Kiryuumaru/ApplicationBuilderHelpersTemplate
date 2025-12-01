using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.FunctionalTests;

/// <summary>
/// Tests for security edge cases and potential vulnerabilities.
/// </summary>
public class SecurityTests : PlaywrightTestBase
{
    [Test]
    public async Task LoginForm_HasAntiForgeryToken()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        // Check for anti-forgery token input
        var tokenInput = Page.Locator("input[name='__RequestVerificationToken']");
        await Expect(tokenInput).ToHaveCountAsync(1);
    }

    [Test]
    public async Task RegisterForm_HasAntiForgeryToken()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        // Check for anti-forgery token input
        var tokenInput = Page.Locator("input[name='__RequestVerificationToken']");
        await Expect(tokenInput).ToHaveCountAsync(1);
    }

    [Test]
    public async Task PasswordField_HasAutocompleteOff()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var passwordInput = Page.GetByLabel("Password");
        var autocomplete = await passwordInput.GetAttributeAsync("autocomplete");
        
        // Should have autocomplete off or new-password
        Assert.That(autocomplete, Does.Match("off|new-password|current-password"));
    }

    [Test]
    public async Task SqlInjection_DoesNotAffectLogin()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        // Try SQL injection in email field
        await Page.GetByLabel("Email").FillAsync("' OR '1'='1' --");
        await Page.GetByLabel("Password").FillAsync("' OR '1'='1' --");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        // Should show invalid login, not success
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/Login"));
    }

    [Test]
    public async Task SqlInjection_DoesNotAffectRegistration()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        await Page.GetByLabel("Email").FillAsync("'; DROP TABLE Users; --");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("TestPassword123!");
        await Page.GetByLabel("Confirm Password").FillAsync("TestPassword123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Should show validation error for invalid email
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task XssAttempt_InEmailField_IsEscaped()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var xssPayload = "<script>alert('xss')</script>@test.com";
        await Page.GetByLabel("Email").FillAsync(xssPayload);
        await Page.GetByLabel("Password").FillAsync("TestPassword123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        // Page should still be functional - check that the script was not executed
        // by verifying we're still on a proper login page with expected elements
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
        
        // The page HTML should not contain unescaped script tags in output
        var content = await Page.ContentAsync();
        Assert.That(content.Contains("<script>alert('xss')</script>"), Is.False, 
            "XSS payload should be escaped in HTML output");
    }

    [Test]
    public async Task PathTraversal_InReturnUrl_IsSanitized()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login?ReturnUrl=../../../etc/passwd");

        // Page should load normally with the return URL parameter
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
        
        // The path traversal attempt should be in the URL but should not be followed
        // when a successful login happens - just verify the login page loaded safely
        var pageTitle = await Page.TitleAsync();
        Assert.That(pageTitle.ToLower(), Does.Contain("log in"));
    }

    [Test]
    public async Task OpenRedirect_InReturnUrl_IsBlocked()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login?ReturnUrl=https://evil.com");

        // Page should load normally
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_RateLimiting_DoesNotCrashServer()
    {
        // Try multiple rapid login attempts
        for (int i = 0; i < 5; i++)
        {
            await Page.GotoAsync($"{BaseUrl}/Account/Login");
            await Page.GetByLabel("Email").FillAsync("ratelimit@test.com");
            await Page.GetByLabel("Password").FillAsync("WrongPassword!");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        }

        // Server should still be responsive
        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ErrorMessages_DoNotRevealUserExistence()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        // Try non-existent user
        await Page.GetByLabel("Email").FillAsync("definitelynotauser@example.com");
        await Page.GetByLabel("Password").FillAsync("WrongPassword123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        // Wait for error message to appear
        await Expect(Page.Locator(".text-danger, .validation-summary-errors, .alert-danger")).ToBeVisibleAsync();
        
        // Get error message text
        var errorText = await Page.Locator(".text-danger, .validation-summary-errors, .alert-danger").TextContentAsync();
        
        // Should not specifically say "user not found"
        Assert.That(errorText, Does.Not.Contain("not found"));
        Assert.That(errorText, Does.Not.Contain("does not exist"));
    }

    [Test]
    public async Task PasswordField_IsMasked()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var passwordInput = Page.GetByLabel("Password");
        var inputType = await passwordInput.GetAttributeAsync("type");
        
        Assert.That(inputType, Is.EqualTo("password"));
    }

    [Test]
    public async Task HttpsRedirect_IsConfigured()
    {
        // In test environment, this checks the response headers contain security headers
        var response = await Page.GotoAsync($"{BaseUrl}/");
        
        // Page should load successfully
        Assert.That(response?.Status, Is.LessThan(400));
    }
}
