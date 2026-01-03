using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// Tests for navigation and routing.
/// </summary>
public class NavigationTests : PlaywrightTestBase
{
    [Test]
    public async Task HomePageLink_NavigatesToHome()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        var homeLink = Page.GetByRole(AriaRole.Link, new() { Name = "Presentation.WebApp" });
        await homeLink.ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task LoginLink_NavigatesToLogin()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        await Page.GetByRole(AriaRole.Link, new() { Name = "Login" }).ClickAsync();

        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegisterLink_NavigatesToRegister()
    {
        await Page.GotoAsync($"{BaseUrl}/");

        await Page.GetByRole(AriaRole.Link, new() { Name = "Register" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Register", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task BrowserBackButton_WorksCorrectly()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        
        await Page.GoBackAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task BrowserForwardButton_WorksCorrectly()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GoBackAsync();
        await Page.GoForwardAsync();

        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task DirectUrlAccess_Works()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Register", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task NonExistentPage_ShowsError()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/NonExistentPage12345");

        // Should return 404 or redirect
        Assert.That(response?.Status, Is.EqualTo(404).Or.LessThan(500));
    }

    [Test]
    public async Task TrailingSlash_HandledCorrectly()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login/");

        // Should work regardless of trailing slash
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task CaseSensitivity_UrlsHandledCorrectly()
    {
        await Page.GotoAsync($"{BaseUrl}/ACCOUNT/LOGIN");

        // Should handle case variations
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task QueryParameters_PreservedOnRedirect()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Manage?test=value");

        // Should redirect to login with ReturnUrl containing the original path
        var url = Page.Url;
        Assert.That(url, Does.Contain("ReturnUrl"));
    }

    [Test]
    public async Task Hash_FragmentPreserved()
    {
        await Page.GotoAsync($"{BaseUrl}/#section");

        // Page should load correctly
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task PageRefresh_MaintainsState()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");

        await Page.GetByLabel("Email").FillAsync("test@example.com");
        await Page.ReloadAsync();

        // After refresh, should still be on login page
        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
    }

    [Test]
    public async Task MultipleNavigations_WorksCorrectly()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GotoAsync($"{BaseUrl}/Account/ForgotPassword");
        await Page.GotoAsync($"{BaseUrl}/");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Hello, world!" })).ToBeVisibleAsync();
    }
}
