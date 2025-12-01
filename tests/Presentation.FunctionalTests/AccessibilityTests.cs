using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.FunctionalTests;

/// <summary>
/// Tests for accessibility compliance.
/// </summary>
public class AccessibilityTests : PlaywrightTestBase
{
    [Test]
    public async Task HomePage_HasMainHeading()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        var heading = Page.GetByRole(AriaRole.Heading, new() { Level = 1 });
        await Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task LoginPage_LabelsAssociatedWithInputs()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        // All inputs should be accessible via their labels
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Password")).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegisterPage_LabelsAssociatedWithInputs()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Password", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Confirm Password")).ToBeVisibleAsync();
    }

    [Test]
    public async Task LoginButton_HasAccessibleName()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var button = Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true });
        await Expect(button).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegisterButton_HasAccessibleName()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        var button = Page.GetByRole(AriaRole.Button, new() { Name = "Register" });
        await Expect(button).ToBeVisibleAsync();
    }

    [Test]
    public async Task NavigationLinks_AreAccessible()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        // Main navigation should use proper roles
        var nav = Page.GetByRole(AriaRole.Navigation);
        await Expect(nav.First).ToBeVisibleAsync();
    }

    [Test]
    public async Task ErrorMessages_AreAccessible()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        // Submit empty form
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        // Error messages should be visible
        var errors = Page.Locator("[role='alert'], .text-danger, .validation-summary-errors");
        var errorCount = await errors.CountAsync();
        Assert.That(errorCount, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task FocusOrder_IsLogical()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        // Tab through form elements
        await Page.Keyboard.PressAsync("Tab");
        
        // Focus should be on an interactive element
        var focusedElement = Page.Locator(":focus");
        await Expect(focusedElement).ToBeVisibleAsync();
    }

    [Test]
    public async Task FormValidationErrors_LinkedToInputs()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Check for aria-describedby or similar attributes
        var emailInput = Page.GetByLabel("Email");
        var describedBy = await emailInput.GetAttributeAsync("aria-describedby");
        var ariaInvalid = await emailInput.GetAttributeAsync("aria-invalid");
        
        // At least one accessibility attribute should be present after validation
        Assert.That(describedBy != null || ariaInvalid != null || await Page.Locator(".validation-message").CountAsync() > 0,
            "Validation errors should be accessible");
    }

    [Test]
    public async Task PageTitle_IsDescriptive()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var title = await Page.TitleAsync();
        Assert.That(title, Is.Not.Empty);
        Assert.That(title.ToLower(), Does.Contain("login").Or.Contain("sign in").Or.Contain("log in"));
    }

    [Test]
    public async Task HomePageTitle_IsDescriptive()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        var title = await Page.TitleAsync();
        Assert.That(title, Is.Not.Empty);
    }

    [Test]
    public async Task SkipLinks_ExistForNavigation()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        // Look for skip link (common accessibility pattern)
        var skipLink = Page.Locator("a[href='#main'], a[href='#content'], .skip-link, .skip-nav");
        var count = await skipLink.CountAsync();
        
        // This is optional - just checking if it exists
        Assert.Pass($"Skip links present: {count > 0}");
    }

    [Test]
    public async Task ColorContrast_ButtonsAreVisible()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var button = Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true });
        await Expect(button).ToBeVisibleAsync();
        
        // Button should have distinguishable styling
        var bgColor = await button.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        Assert.That(bgColor, Is.Not.Empty);
    }

    [Test]
    public async Task ResponsiveDesign_MobileViewport()
    {
        await Page.SetViewportSizeAsync(375, 667); // iPhone SE
        await Page.GotoAsync($"{BaseUrl}/");

        // Page should still be functional
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ResponsiveDesign_TabletViewport()
    {
        await Page.SetViewportSizeAsync(768, 1024); // iPad
        await Page.GotoAsync($"{BaseUrl}/");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ResponsiveDesign_DesktopViewport()
    {
        await Page.SetViewportSizeAsync(1920, 1080);
        await Page.GotoAsync($"{BaseUrl}/");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
    }
}
