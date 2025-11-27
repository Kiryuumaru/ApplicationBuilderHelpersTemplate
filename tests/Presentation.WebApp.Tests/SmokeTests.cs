using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.Tests;

public class SmokeTests : PlaywrightTestBase
{
    [Test]
    public async Task HomePage_Loads()
    {
        await Page.GotoAsync(BaseUrl);
        await Expect(Page).ToHaveTitleAsync("Home");
    }

    [Test]
    public async Task LoginPage_Loads()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Log in", Exact = true })).ToBeVisibleAsync();
    }
}
