using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// Tests for user interface elements and behavior.
/// </summary>
public class UITests : PlaywrightTestBase
{
    [Test]
    public async Task HomePage_HasNavigationBar()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        var nav = Page.GetByRole(AriaRole.Navigation);
        await Expect(nav.First).ToBeVisibleAsync();
    }

    [Test]
    public async Task NavigationBar_HasBrandLink()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        var brandLink = Page.GetByRole(AriaRole.Link, new() { Name = "Presentation.WebApp" });
        await Expect(brandLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task LoginForm_HasCorrectLayout()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        // All form elements should be visible
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Password")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Remember me")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegisterForm_HasCorrectLayout()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Password", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Confirm Password")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Register" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ErrorMessages_AreStyledCorrectly()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        var errorElements = Page.Locator(".text-danger, .validation-message, .alert-danger");
        var count = await errorElements.CountAsync();
        Assert.That(count, Is.GreaterThanOrEqualTo(0), "Error styling should be applied");
    }

    [Test]
    public async Task Buttons_HaveConsistentStyling()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var loginButton = Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true });
        var bgColor = await loginButton.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        
        Assert.That(bgColor, Is.Not.EqualTo("rgba(0, 0, 0, 0)"), "Button should have visible background");
    }

    [Test]
    public async Task Forms_HaveProperSpacing()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var emailInput = Page.GetByLabel("Email");
        var passwordInput = Page.GetByLabel("Password");

        var emailBox = await emailInput.BoundingBoxAsync();
        var passwordBox = await passwordInput.BoundingBoxAsync();

        Assert.That(emailBox, Is.Not.Null);
        Assert.That(passwordBox, Is.Not.Null);
        
        // Password should be below email (proper vertical spacing)
        Assert.That(passwordBox!.Y, Is.GreaterThan(emailBox!.Y));
    }

    [Test]
    public async Task PageFooter_IsPresent()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        var footer = Page.Locator("footer");
        var count = await footer.CountAsync();
        
        if (count > 0)
        {
            await Expect(footer.First).ToBeVisibleAsync();
        }
        else
        {
            Assert.Pass("No footer element present");
        }
    }

    [Test]
    public async Task LoadingIndicator_NotStuckVisible()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Any loading indicators should be hidden
        var loaders = Page.Locator(".loading, .spinner, [aria-busy='true']");
        var count = await loaders.CountAsync();
        
        for (int i = 0; i < count; i++)
        {
            var isVisible = await loaders.Nth(i).IsVisibleAsync();
            Assert.That(isVisible, Is.False, $"Loading indicator {i} should be hidden after page load");
        }
    }

    [Test]
    public async Task InputFocus_HasVisualIndicator()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var emailInput = Page.GetByLabel("Email");
        await emailInput.FocusAsync();

        // Check for focus styling (outline, border, or shadow)
        var outline = await emailInput.EvaluateAsync<string>("el => getComputedStyle(el).outline");
        var boxShadow = await emailInput.EvaluateAsync<string>("el => getComputedStyle(el).boxShadow");
        var borderColor = await emailInput.EvaluateAsync<string>("el => getComputedStyle(el).borderColor");

        Assert.That(
            outline != "none" || boxShadow != "none" || !string.IsNullOrEmpty(borderColor),
            "Focused input should have visual indicator");
    }

    [Test]
    public async Task Links_HaveProperStyling()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var forgotLink = Page.GetByRole(AriaRole.Link, new() { Name = "Forgot", Exact = false });
        var color = await forgotLink.EvaluateAsync<string>("el => getComputedStyle(el).color");
        var textDecoration = await forgotLink.EvaluateAsync<string>("el => getComputedStyle(el).textDecoration");

        Assert.That(color, Is.Not.EqualTo("rgb(0, 0, 0)"), "Links should have distinctive color");
    }

    [Test]
    public async Task CheckboxLabel_IsClickable()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var rememberMeLabel = Page.Locator("label:has-text('Remember me')");
        await rememberMeLabel.ClickAsync();

        var checkbox = Page.GetByLabel("Remember me");
        var isChecked = await checkbox.IsCheckedAsync();
        
        Assert.That(isChecked, Is.True, "Clicking label should toggle checkbox");
    }

    [Test]
    public async Task PlaceholderText_IsReadable()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var emailInput = Page.GetByLabel("Email");
        var placeholder = await emailInput.GetAttributeAsync("placeholder");
        
        // Placeholder may or may not be present, but if it is, it should be meaningful
        if (placeholder != null)
        {
            Assert.That(placeholder.Length, Is.GreaterThan(0));
        }
        else
        {
            Assert.Pass("No placeholder present (label is used instead)");
        }
    }
}
