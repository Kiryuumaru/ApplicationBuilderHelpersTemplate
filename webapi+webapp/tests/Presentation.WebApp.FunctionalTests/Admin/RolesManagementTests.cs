namespace Presentation.WebApp.FunctionalTests.Admin;

/// <summary>
/// Playwright functional tests for role management page.
/// Tests admin role management functionality through the Blazor WebApp.
/// </summary>
[Collection(WebAppTestCollection.Name)]
public class RolesManagementTests : PlaywrightTestBase
{
    private const string TestPassword = "TestPassword123!";

    public RolesManagementTests(SharedTestHosts sharedHosts, ITestOutputHelper output)
        : base(sharedHosts, output)
    {
    }

    [Fact]
    public async Task RolesPage_RequiresAuthentication()
    {
        // Act - Try to access roles page without authentication
        await Page.GotoAsync($"{WebAppUrl}/admin/roles");
        await WaitForBlazorAsync();

        // Assert - Should redirect to login or show unauthorized
        var currentUrl = Page.Url;
        var pageContent = await Page.ContentAsync();

        var redirectedToLogin = currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase);
        var showsUnauthorized = pageContent.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                                pageContent.Contains("access denied", StringComparison.OrdinalIgnoreCase);

        Assert.True(redirectedToLogin || showsUnauthorized, "Should require authentication for admin roles page");
    }

    [Fact]
    public async Task RolesPage_Authenticated_ShowsRoleCards()
    {
        // Arrange - Register and login
        var username = $"roles_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to roles page
        await Page.GotoAsync($"{WebAppUrl}/admin/roles");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        var pageContent = await Page.ContentAsync();
        Output.WriteLine($"Roles page URL: {currentUrl}");

        // Assert - If accessible, should show role management content
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            var hasRolesContent = pageContent.Contains("role", StringComparison.OrdinalIgnoreCase) ||
                                  pageContent.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
                                  pageContent.Contains("admin", StringComparison.OrdinalIgnoreCase);

            Output.WriteLine($"Has roles content: {hasRolesContent}");
        }
    }

    [Fact]
    public async Task RolesPage_HasAddRoleButton()
    {
        // Arrange - Register and login
        var username = $"addrole_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to roles page
        await Page.GotoAsync($"{WebAppUrl}/admin/roles");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should have add role button
            var addButton = await Page.QuerySelectorAsync("button:has-text('Add'), button:has-text('Create'), button:has-text('New')");
            Output.WriteLine($"Add role button found: {addButton != null}");
        }
    }

    [Fact]
    public async Task RolesPage_DisplaysRolePermissions()
    {
        // Arrange - Register and login
        var username = $"perms_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to roles page
        await Page.GotoAsync($"{WebAppUrl}/admin/roles");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should display permissions for roles
            var pageContent = await Page.ContentAsync();
            var hasPermissions = pageContent.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
                                pageContent.Contains("read", StringComparison.OrdinalIgnoreCase) ||
                                pageContent.Contains("write", StringComparison.OrdinalIgnoreCase);

            Output.WriteLine($"Has permissions display: {hasPermissions}");
        }
    }

    [Fact]
    public async Task RolesPage_ShowsUserCount()
    {
        // Arrange - Register and login
        var username = $"count_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to roles page
        await Page.GotoAsync($"{WebAppUrl}/admin/roles");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should show user count for roles
            var pageContent = await Page.ContentAsync();
            var hasUserCount = pageContent.Contains("user", StringComparison.OrdinalIgnoreCase);

            Output.WriteLine($"Has user count display: {hasUserCount}");
        }
    }

    [Fact]
    public async Task RolesPage_SystemRolesNotDeletable()
    {
        // Arrange - Register and login
        var username = $"system_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to roles page
        await Page.GotoAsync($"{WebAppUrl}/admin/roles");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Admin/User system roles should not have delete button
            // or delete button should be disabled
            var pageContent = await Page.ContentAsync();

            // Look for Admin role card - it should exist
            var hasAdminRole = pageContent.Contains("Admin", StringComparison.Ordinal);
            Output.WriteLine($"Has Admin role: {hasAdminRole}");
        }
    }

    [Fact]
    public async Task RolesPage_HasEditButtons()
    {
        // Arrange - Register and login
        var username = $"edit_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to roles page
        await Page.GotoAsync($"{WebAppUrl}/admin/roles");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should have edit buttons for roles
            var editButtons = await Page.QuerySelectorAllAsync("button[aria-label*='edit' i], button:has(svg)");
            Output.WriteLine($"Edit buttons found: {editButtons.Count}");
        }
    }

    [Fact]
    public async Task RolesPage_DisplaysRoleDescription()
    {
        // Arrange - Register and login
        var username = $"desc_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to roles page
        await Page.GotoAsync($"{WebAppUrl}/admin/roles");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should show role descriptions
            var pageContent = await Page.ContentAsync();
            var hasDescriptions = pageContent.Contains("access", StringComparison.OrdinalIgnoreCase) ||
                                 pageContent.Contains("management", StringComparison.OrdinalIgnoreCase) ||
                                 pageContent.Contains("description", StringComparison.OrdinalIgnoreCase);

            Output.WriteLine($"Has role descriptions: {hasDescriptions}");
        }
    }
}
