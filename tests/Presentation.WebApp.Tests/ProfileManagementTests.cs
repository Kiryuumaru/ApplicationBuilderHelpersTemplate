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

        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
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

        await Expect(Page.GetByLabel("Current password")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("New password", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Confirm new password")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChangePassword_WithCorrectCurrentPassword_Succeeds()
    {
        var email = $"profile3_{Guid.NewGuid():N}@test.com";
        var oldPassword = "TestPassword123!";
        var newPassword = "NewPassword456!";
        
        await RegisterAndLoginUserAsync(email, oldPassword);
        await Page.GotoAsync($"{BaseUrl}/Account/Manage/ChangePassword");

        await Page.GetByLabel("Current password").FillAsync(oldPassword);
        await Page.GetByLabel("New password", new() { Exact = true }).FillAsync(newPassword);
        await Page.GetByLabel("Confirm new password").FillAsync(newPassword);
        
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

        await Page.GetByLabel("Current password").FillAsync("WrongPassword!");
        await Page.GetByLabel("New password", new() { Exact = true }).FillAsync("NewPassword456!");
        await Page.GetByLabel("Confirm new password").FillAsync("NewPassword456!");
        
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

        await Page.GetByLabel("Current password").FillAsync("TestPassword123!");
        await Page.GetByLabel("New password", new() { Exact = true }).FillAsync("NewPassword456!");
        await Page.GetByLabel("Confirm new password").FillAsync("DifferentPassword789!");
        
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

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Two-factor", Exact = false })).ToBeVisibleAsync();
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
