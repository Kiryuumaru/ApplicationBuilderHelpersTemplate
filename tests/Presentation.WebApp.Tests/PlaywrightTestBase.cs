using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Presentation.WebApp.Tests;

public abstract class PlaywrightTestBase : PageTest
{
    protected WebAppTestFixture Fixture { get; private set; } = null!;
    protected string BaseUrl => Fixture.ServerAddress;

    [OneTimeSetUp]
    public void GlobalSetup()
    {
        Fixture = new WebAppTestFixture();
    }

    [OneTimeTearDown]
    public void GlobalTeardown()
    {
        Fixture.Dispose();
    }

    /// <summary>
    /// Registers a new user and ensures they are logged in.
    /// Since RequireConfirmedAccount is false, users are auto-logged in after registration.
    /// </summary>
    protected async Task RegisterAndLoginUserAsync(string email, string password)
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Since RequireConfirmedAccount is false, user is auto-logged in and redirected to home page
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Registers a new user, confirms their email, and logs them in.
    /// Use this when you need the user's email to be confirmed.
    /// </summary>
    protected async Task RegisterConfirmAndLoginUserAsync(string email, string password)
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // User is auto-logged in, but if there's a confirmation link, click it to confirm email
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Navigate to profile and confirm email if needed
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/Email");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var confirmLink = Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm" });
        if (await confirmLink.CountAsync() > 0)
        {
            await confirmLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
    }
}
