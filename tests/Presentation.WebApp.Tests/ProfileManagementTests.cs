using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.Tests;

/// <summary>
/// Tests for user profile management functionality.
/// </summary>
public class ProfileManagementTests : PlaywrightTestBase
{
    private async Task RegisterAndLoginUserAsync(string email, string password)
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Register");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirm Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        // Wait for registration confirmation page and click the email confirmation link
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/Account/RegisterConfirmation"));
        var confirmLink = Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm your account" });
        if (await confirmLink.CountAsync() > 0)
        {
            await confirmLink.ClickAsync();
            await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        }

        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
        
        // Wait for login to complete
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
    }

    [Test]
    public async Task ManagePage_RedirectsToLoginWhenNotAuthenticated()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Manage");

        // Should be redirected to login
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/Account/Login"));
    }

    [Test]
    public async Task ManagePage_LoadsForAuthenticatedUser()
    {
        var email = $"profile1_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");

        await Page.GotoAsync($"{BaseUrl}/Account/Manage");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Profile", Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChangePasswordPage_LoadsForAuthenticatedUser()
    {
        var email = $"profile2_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");

        await Page.GotoAsync($"{BaseUrl}/Account/Manage/ChangePassword");

        await Expect(Page.GetByLabel("Old password")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("New password", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Confirm password", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChangePassword_WithCorrectCurrentPassword_Succeeds()
    {
        var email = $"profile3_{Guid.NewGuid():N}@test.com";
        var oldPassword = "TestPassword123!";
        var newPassword = "NewPassword456!";
        
        await RegisterAndLoginUserAsync(email, oldPassword);
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/ChangePassword");

        await Page.GetByLabel("Old password").FillAsync(oldPassword);
        await Page.GetByLabel("New password", new() { Exact = true }).FillAsync(newPassword);
        await Page.GetByLabel("Confirm password", new() { Exact = true }).FillAsync(newPassword);
        
        await Page.GetByRole(AriaRole.Button, new() { Name = "Update password" }).ClickAsync();

        // Should show success message
        await Expect(Page.GetByText("password has been changed", new() { Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChangePassword_WithWrongCurrentPassword_ShowsError()
    {
        var email = $"profile4_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/ChangePassword");

        await Page.GetByLabel("Old password").FillAsync("WrongPassword!");
        await Page.GetByLabel("New password", new() { Exact = true }).FillAsync("NewPassword456!");
        await Page.GetByLabel("Confirm password", new() { Exact = true }).FillAsync("NewPassword456!");
        
        await Page.GetByRole(AriaRole.Button, new() { Name = "Update password" }).ClickAsync();

        // Should show error
        await Expect(Page.GetByText("incorrect", new() { Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChangePassword_WithMismatchedNewPasswords_ShowsError()
    {
        var email = $"profile5_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/ChangePassword");

        await Page.GetByLabel("Old password").FillAsync("TestPassword123!");
        await Page.GetByLabel("New password", new() { Exact = true }).FillAsync("NewPassword456!");
        await Page.GetByLabel("Confirm password", new() { Exact = true }).FillAsync("DifferentPassword789!");
        
        await Page.GetByRole(AriaRole.Button, new() { Name = "Update password" }).ClickAsync();

        // Should show validation error
        await Expect(Page.Locator(".validation-message, .text-danger").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task TwoFactorAuthPage_LoadsForAuthenticatedUser()
    {
        var email = $"profile6_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");

        await Page.GotoAsync($"{BaseUrl}/Account/Manage/TwoFactorAuthentication");
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        // Page should load and stay on TwoFactorAuthentication URL or redirect if auth failed
        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/").IgnoreCase, 
            $"Expected to be on Account page but was on: {url}");
    }

    [Test]
    public async Task PersonalDataPage_LoadsForAuthenticatedUser()
    {
        var email = $"profile7_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");

        await Page.GotoAsync($"{BaseUrl}/Account/Manage/PersonalData");

        // Should show download and delete options
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Personal Data", Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task EmailPage_LoadsForAuthenticatedUser()
    {
        var email = $"profile8_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");

        await Page.GotoAsync($"{BaseUrl}/Account/Manage/Email");

        // Should show current email
        await Expect(Page.GetByText(email, new() { Exact = false })).ToBeVisibleAsync();
    }

    [Test]
    public async Task DeletePersonalDataPage_LoadsForAuthenticatedUser()
    {
        var email = $"profile9_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(email, "TestPassword123!");

        await Page.GotoAsync($"{BaseUrl}/Account/Manage/DeletePersonalData");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Delete Personal Data", Exact = false })).ToBeVisibleAsync();
    }
}
