using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.FunctionalTests;

/// <summary>
/// Tests for performance characteristics.
/// </summary>
public class PerformanceTests : PlaywrightTestBase
{
    [Test]
    public async Task HomePage_LoadsWithinThreshold()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var response = await Page.GotoAsync($"{BaseUrl}/");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        
        stopwatch.Stop();
        
        Assert.That(response?.Status, Is.EqualTo(200));
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000), "Homepage should load within 5 seconds");
    }

    [Test]
    public async Task LoginPage_LoadsWithinThreshold()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var response = await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        
        stopwatch.Stop();
        
        Assert.That(response?.Status, Is.EqualTo(200));
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000), "Login page should load within 5 seconds");
    }

    [Test]
    public async Task RegisterPage_LoadsWithinThreshold()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var response = await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        
        stopwatch.Stop();
        
        Assert.That(response?.Status, Is.EqualTo(200));
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000), "Register page should load within 5 seconds");
    }

    [Test]
    public async Task RegistrationFlow_CompletesWithinThreshold()
    {
        var email = $"perf_{Guid.NewGuid():N}@test.com";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync("TestPassword123!");
        await Page.GetByLabel("Confirm Password").FillAsync("TestPassword123!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        stopwatch.Stop();
        
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10000), "Registration should complete within 10 seconds");
    }

    [Test]
    public async Task LoginFlow_CompletesWithinThreshold()
    {
        var email = $"perflogin_{Guid.NewGuid():N}@test.com";
        var password = "TestPassword123!";
        
        // First register
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        
        // Now time the login
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        stopwatch.Stop();
        
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000), "Login should complete within 5 seconds");
    }

    [Test]
    public async Task MultiplePageNavigations_PerformWell()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GotoAsync($"{BaseUrl}/Account/ForgotPassword");
        await Page.GotoAsync($"{BaseUrl}/");
        
        stopwatch.Stop();
        
        // Average of less than 2 seconds per page
        Assert.That(stopwatch.ElapsedMilliseconds / 5, Is.LessThan(2000), "Pages should load quickly on average");
    }

    [Test]
    public async Task ConcurrentRequests_HandleWell()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Create multiple page contexts and load simultaneously
        var tasks = Enumerable.Range(0, 3).Select(async i =>
        {
            var newPage = await Page.Context.NewPageAsync();
            await newPage.GotoAsync($"{BaseUrl}/");
            await newPage.CloseAsync();
        });
        
        await Task.WhenAll(tasks);
        
        stopwatch.Stop();
        
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(15000), "Concurrent requests should complete within 15 seconds");
    }

    [Test]
    public async Task PageRefresh_IsFast()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        
        stopwatch.Stop();
        
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(3000), "Page refresh should be fast");
    }

    [Test]
    public async Task FormInteraction_IsResponsive()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        await Page.GetByLabel("Email").FillAsync("test@example.com");
        await Page.GetByLabel("Password").FillAsync("TestPassword123!");
        
        stopwatch.Stop();
        
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000), "Form input should be responsive");
    }

    [Test]
    public async Task ValidationErrors_AppearQuickly()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();
        
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
        
        stopwatch.Stop();
        
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(2000), "Validation errors should appear quickly");
    }
}
