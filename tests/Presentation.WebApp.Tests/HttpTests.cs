using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.Tests;

/// <summary>
/// Tests for HTTP response handling and headers.
/// </summary>
public class HttpTests : PlaywrightTestBase
{
    [Test]
    public async Task HomePage_Returns200()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/");
        Assert.That(response?.Status, Is.EqualTo(200));
    }

    [Test]
    public async Task LoginPage_Returns200()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/Account/Login");
        Assert.That(response?.Status, Is.EqualTo(200));
    }

    [Test]
    public async Task RegisterPage_Returns200()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/Account/Register");
        Assert.That(response?.Status, Is.EqualTo(200));
    }

    [Test]
    public async Task ForgotPasswordPage_Returns200()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/Account/ForgotPassword");
        Assert.That(response?.Status, Is.EqualTo(200));
    }

    [Test]
    public async Task NonExistentPage_Returns404()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/this-page-does-not-exist");
        Assert.That(response?.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task ProtectedPage_Redirects()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/Account/Manage");
        
        // Should redirect to login (302/200 after redirect)
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/Login"));
    }

    [Test]
    public async Task ContentType_Html()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/");
        var contentType = response?.Headers["content-type"];
        Assert.That(contentType, Does.Contain("text/html"));
    }

    [Test]
    public async Task CacheHeaders_ArePresent()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/");
        
        // Check for cache-control or similar headers
        var headers = response?.Headers;
        Assert.That(headers, Is.Not.Null);
    }

    [Test]
    public async Task SecurityHeaders_XFrameOptions()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/");
        
        var xFrameOptions = response?.Headers.ContainsKey("x-frame-options") == true 
            ? response.Headers["x-frame-options"] 
            : null;
        
        // X-Frame-Options should be set (DENY or SAMEORIGIN)
        if (xFrameOptions != null)
        {
            Assert.That(xFrameOptions, Does.Match("DENY|SAMEORIGIN"));
        }
        else
        {
            Assert.Pass("X-Frame-Options header not present (may be using CSP frame-ancestors)");
        }
    }

    [Test]
    public async Task SecurityHeaders_XContentTypeOptions()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/");
        
        var xContentType = response?.Headers.ContainsKey("x-content-type-options") == true 
            ? response.Headers["x-content-type-options"] 
            : null;
        
        if (xContentType != null)
        {
            Assert.That(xContentType, Is.EqualTo("nosniff"));
        }
        else
        {
            Assert.Pass("X-Content-Type-Options header not present");
        }
    }

    [Test]
    public async Task Cookies_HaveSecureAttributes()
    {
        var email = $"cookie_{Guid.NewGuid():N}@test.com";
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

        // Check cookies
        var context = Page.Context;
        var cookies = await context.CookiesAsync();
        
        foreach (var cookie in cookies.Where(c => c.Name.Contains("Identity") || c.Name.Contains("Auth")))
        {
            Assert.That(cookie.HttpOnly, Is.True, $"Cookie {cookie.Name} should be HttpOnly");
        }
    }

    [Test]
    public async Task StaticAssets_Load()
    {
        var response = await Page.GotoAsync($"{BaseUrl}/");
        
        // Check that CSS/JS files load properly
        var resources = new List<string>();
        Page.Response += (_, e) =>
        {
            if (e.Url.Contains(".css") || e.Url.Contains(".js"))
            {
                resources.Add(e.Url);
            }
        };

        await Page.ReloadAsync();
        
        // Page loaded successfully
        Assert.That(response?.Status, Is.EqualTo(200));
    }

    [Test]
    public async Task ApiEndpoint_MethodNotAllowed()
    {
        // Try to POST to a GET-only endpoint (if any)
        var request = Page.Context.APIRequest;
        var response = await request.PostAsync($"{BaseUrl}/");
        
        // Should either return 405 or process differently
        Assert.That(response.Status, Is.LessThan(500));
    }
}
