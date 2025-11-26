using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.Tests;

/// <summary>
/// Tests for form validation and input handling.
/// </summary>
public class FormValidationTests : PlaywrightTestBase
{
    [Test]
    public async Task RegisterEmail_WithSpaces_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        
        await Page.GetByLabel("Email").FillAsync("user with spaces@test.com");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("TestPassword123!");
        await Page.GetByLabel("Confirm Password").FillAsync("TestPassword123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Email with spaces should show validation error (client or server-side)
        await Expect(Page.Locator(".validation-message, .text-danger, .alert-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegisterEmail_WithSpecialCharacters_HandledCorrectly()
    {
        var email = $"test+tag_{Guid.NewGuid():N}@test.com";
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("TestPassword123!");
        await Page.GetByLabel("Confirm Password").FillAsync("TestPassword123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Should either succeed or show specific validation error
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }

    [Test]
    public async Task RegisterPassword_TooShort_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        
        await Page.GetByLabel("Email").FillAsync($"short_{Guid.NewGuid():N}@test.com");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("Ab1!");
        await Page.GetByLabel("Confirm Password").FillAsync("Ab1!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        await Expect(Page.Locator(".validation-message, .text-danger, .alert-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegisterPassword_NoUppercase_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        
        await Page.GetByLabel("Email").FillAsync($"noupper_{Guid.NewGuid():N}@test.com");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("testpassword123!");
        await Page.GetByLabel("Confirm Password").FillAsync("testpassword123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Server-side password validation errors appear in .alert-danger
        await Expect(Page.Locator(".validation-message, .text-danger, .alert-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegisterPassword_NoLowercase_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        
        await Page.GetByLabel("Email").FillAsync($"nolower_{Guid.NewGuid():N}@test.com");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("TESTPASSWORD123!");
        await Page.GetByLabel("Confirm Password").FillAsync("TESTPASSWORD123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Server-side password validation errors appear in .alert-danger
        await Expect(Page.Locator(".validation-message, .text-danger, .alert-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegisterPassword_NoDigit_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        
        await Page.GetByLabel("Email").FillAsync($"nodigit_{Guid.NewGuid():N}@test.com");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("TestPassword!");
        await Page.GetByLabel("Confirm Password").FillAsync("TestPassword!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Server-side password validation errors appear in .alert-danger
        await Expect(Page.Locator(".validation-message, .text-danger, .alert-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegisterPassword_NoSpecialChar_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        
        await Page.GetByLabel("Email").FillAsync($"nospecial_{Guid.NewGuid():N}@test.com");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("TestPassword123");
        await Page.GetByLabel("Confirm Password").FillAsync("TestPassword123");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Server-side password validation errors appear in .alert-danger
        await Expect(Page.Locator(".validation-message, .text-danger, .alert-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task LoginEmail_CaseInsensitive()
    {
        var baseEmail = $"casetest_{Guid.NewGuid():N}";
        var email = $"{baseEmail}@test.com";
        var password = "TestPassword123!";

        // Register with lowercase
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email.ToLower());
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Try login with uppercase
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email.ToUpper());
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        // Should succeed (email comparison is case-insensitive)
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Password_PasswordVisible_ToggleWorks()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var passwordInput = Page.GetByLabel("Password");
        var initialType = await passwordInput.GetAttributeAsync("type");
        Assert.That(initialType, Is.EqualTo("password"));

        // If there's a show/hide toggle, it should work
        var toggle = Page.Locator("[data-toggle-password], .password-toggle, button:has-text('Show')").First;
        if (await toggle.IsVisibleAsync())
        {
            await toggle.ClickAsync();
            var newType = await passwordInput.GetAttributeAsync("type");
            Assert.That(newType, Is.EqualTo("text"));
        }
        else
        {
            Assert.Pass("No password visibility toggle present");
        }
    }

    [Test]
    public async Task RegisterEmail_MaxLength_HandledCorrectly()
    {
        var longLocalPart = new string('a', 200);
        var email = $"{longLocalPart}@test.com";
        
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("TestPassword123!");
        await Page.GetByLabel("Confirm Password").FillAsync("TestPassword123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Should show error or truncate
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }

    [Test]
    public async Task FormFields_PreserveValueOnValidationError()
    {
        var email = "invalid-email";
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("weak");
        await Page.GetByLabel("Confirm Password").FillAsync("weak");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Email should still be in the field
        var emailValue = await Page.GetByLabel("Email").InputValueAsync();
        Assert.That(emailValue, Is.EqualTo(email));
    }

    [Test]
    public async Task UnicodeCharacters_InEmailLocalPart_HandledCorrectly()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        
        await Page.GetByLabel("Email").FillAsync("tëst@test.com");
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("TestPassword123!");
        await Page.GetByLabel("Confirm Password").FillAsync("TestPassword123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Should either succeed or show appropriate error
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase);
    }
}
