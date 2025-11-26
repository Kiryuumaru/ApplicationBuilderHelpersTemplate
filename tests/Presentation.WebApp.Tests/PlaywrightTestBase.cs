using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Presentation.WebApp.Tests;

public class PlaywrightTestBase : PageTest
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
}
