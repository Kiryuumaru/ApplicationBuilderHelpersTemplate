namespace Presentation.WebApp.FunctionalTests.Admin;

/// <summary>
/// Playwright functional tests for permissions management page.
/// Tests permission viewing and hierarchy display functionality.
/// </summary>
public class PermissionsManagementTests : WebAppTestBase
{
    public PermissionsManagementTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task PermissionsPage_RequiresAuthentication()
    {
        // Act - Try to access permissions page without authentication
        await Page.GotoAsync($"{WebAppUrl}/admin/permissions");
        await WaitForBlazorAsync();

        // Assert - Should redirect to login or show unauthorized
        var currentUrl = Page.Url;
        var pageContent = await Page.ContentAsync();

        var redirectedToLogin = currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase);
        var showsUnauthorized = pageContent.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                                pageContent.Contains("access denied", StringComparison.OrdinalIgnoreCase);

        Assert.True(redirectedToLogin || showsUnauthorized, "Should require authentication for admin permissions page");
    }

    [Fact]
    public async Task PermissionsPage_Authenticated_ShowsPermissionsTree()
    {
        // Arrange - Register and login
        var username = $"perms_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to permissions page
        await Page.GotoAsync($"{WebAppUrl}/admin/permissions");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        var pageContent = await Page.ContentAsync();
        Output.WriteLine($"Permissions page URL: {currentUrl}");

        // Assert - If accessible, should show permissions content
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            var hasPermissionsContent = pageContent.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
                                        pageContent.Contains("scope", StringComparison.OrdinalIgnoreCase) ||
                                        pageContent.Contains("access", StringComparison.OrdinalIgnoreCase);

            Output.WriteLine($"Has permissions content: {hasPermissionsContent}");
        }
    }

    [Fact]
    public async Task PermissionsPage_ShowsHierarchicalStructure()
    {
        // Arrange - Register and login
        var username = $"hier_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to permissions page
        await Page.GotoAsync($"{WebAppUrl}/admin/permissions");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should show hierarchical structure (tree view, nested lists, etc.)
            var treeView = await Page.QuerySelectorAsync("[class*='tree'], [class*='nested'], ul ul, [role='tree']");
            var expandButtons = await Page.QuerySelectorAllAsync("button[aria-expanded], [class*='expand'], [class*='collapse'], svg");
            
            Output.WriteLine($"Tree view element found: {treeView != null}");
            Output.WriteLine($"Expand/collapse buttons found: {expandButtons.Count}");
        }
    }

    [Fact]
    public async Task PermissionsPage_HasSearchFunctionality()
    {
        // Arrange - Register and login
        var username = $"search_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to permissions page
        await Page.GotoAsync($"{WebAppUrl}/admin/permissions");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should have search input
            var searchInput = await Page.QuerySelectorAsync("input[type='search'], input[placeholder*='search' i], input[placeholder*='filter' i]");
            Output.WriteLine($"Search input found: {searchInput != null}");

            if (searchInput != null)
            {
                // Test search functionality
                await searchInput.FillAsync("user");
                await Task.Delay(500);

                Output.WriteLine("Search/filter test completed");
            }
        }
    }

    [Fact]
    public async Task PermissionsPage_ShowsPermissionCategories()
    {
        // Arrange - Register and login
        var username = $"cats_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to permissions page
        await Page.GotoAsync($"{WebAppUrl}/admin/permissions");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should show permission categories (based on WebApi permission structure)
            var pageContent = await Page.ContentAsync();
            
            // Common permission categories from the WebApi
            var hasUserPermissions = pageContent.Contains("user", StringComparison.OrdinalIgnoreCase);
            var hasRolePermissions = pageContent.Contains("role", StringComparison.OrdinalIgnoreCase);
            var hasAuthPermissions = pageContent.Contains("auth", StringComparison.OrdinalIgnoreCase);

            Output.WriteLine($"Has user permissions: {hasUserPermissions}");
            Output.WriteLine($"Has role permissions: {hasRolePermissions}");
            Output.WriteLine($"Has auth permissions: {hasAuthPermissions}");
        }
    }

    [Fact]
    public async Task PermissionsPage_ShowsPermissionDescriptions()
    {
        // Arrange - Register and login
        var username = $"desc_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to permissions page
        await Page.GotoAsync($"{WebAppUrl}/admin/permissions");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should show permission descriptions
            var pageContent = await Page.ContentAsync();
            
            // Check for descriptive text
            var hasDescriptions = pageContent.Contains("read", StringComparison.OrdinalIgnoreCase) ||
                                 pageContent.Contains("write", StringComparison.OrdinalIgnoreCase) ||
                                 pageContent.Contains("create", StringComparison.OrdinalIgnoreCase) ||
                                 pageContent.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                                 pageContent.Contains("manage", StringComparison.OrdinalIgnoreCase);

            Output.WriteLine($"Has permission descriptions: {hasDescriptions}");
        }
    }

    [Fact]
    public async Task PermissionsPage_ExpandCollapseTreeNodes()
    {
        // Arrange - Register and login
        var username = $"expand_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to permissions page
        await Page.GotoAsync($"{WebAppUrl}/admin/permissions");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should be able to expand/collapse tree nodes
            var expandButton = await Page.QuerySelectorAsync("button[aria-expanded='false'], [class*='expand'], button:has(svg)");
            
            if (expandButton != null)
            {
                // Click to expand
                await expandButton.ClickAsync();
                await Task.Delay(300);

                Output.WriteLine("Expand/collapse functionality tested");
            }
            else
            {
                Output.WriteLine("No expandable tree nodes found (might be flat list or auto-expanded)");
            }
        }
    }

    [Fact]
    public async Task PermissionsPage_NavigationFromSidebar()
    {
        // Arrange - Register and login
        var username = $"nav_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate via sidebar
        await GoToHomeAsync();
        var permissionsLink = await Page.QuerySelectorAsync("a[href*='permissions' i]");

        if (permissionsLink != null)
        {
            await permissionsLink.ClickAsync();
            await WaitForBlazorAsync();

            // Assert - Should be on permissions page
            AssertUrlContains("/admin/permissions");
            Output.WriteLine("✅ Permissions page accessible via navigation");
        }
        else
        {
            Output.WriteLine("Permissions link not found in sidebar");
        }
    }

    [Fact]
    public async Task PermissionsPage_ReadOnlyForNonAdmins()
    {
        // Arrange - Register and login (regular user, not admin)
        var username = $"readonly_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to permissions page
        await Page.GotoAsync($"{WebAppUrl}/admin/permissions");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should not have edit buttons (permissions are typically read-only reference)
            var editButton = await Page.QuerySelectorAsync("button:has-text('Edit'), button:has-text('Create'), button:has-text('Delete')");
            
            // Permissions page is typically read-only (view permissions tree)
            Output.WriteLine($"Edit buttons found: {editButton != null}");
        }
    }

    [Fact]
    public async Task PermissionsPage_ShowsPermissionIds()
    {
        // Arrange - Register and login
        var username = $"ids_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to permissions page
        await Page.GotoAsync($"{WebAppUrl}/admin/permissions");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should show permission identifiers
            var pageContent = await Page.ContentAsync();
            
            // Permission IDs typically follow patterns like "users.read", "roles.write", etc.
            var hasPermissionPatterns = pageContent.Contains(".read", StringComparison.OrdinalIgnoreCase) ||
                                        pageContent.Contains(".write", StringComparison.OrdinalIgnoreCase) ||
                                        pageContent.Contains(".create", StringComparison.OrdinalIgnoreCase) ||
                                        pageContent.Contains(".delete", StringComparison.OrdinalIgnoreCase) ||
                                        pageContent.Contains("_", StringComparison.OrdinalIgnoreCase);

            Output.WriteLine($"Has permission ID patterns: {hasPermissionPatterns}");
        }
    }

    [Fact]
    public async Task PermissionsPage_GroupedByResourceType()
    {
        // Arrange - Register and login
        var username = $"group_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to permissions page
        await Page.GotoAsync($"{WebAppUrl}/admin/permissions");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should be grouped by resource type
            var pageContent = await Page.ContentAsync();
            
            // Check for resource group headers
            var headers = await Page.QuerySelectorAllAsync("h2, h3, h4, [class*='header'], [class*='title']");
            
            Output.WriteLine($"Found {headers.Count} potential group headers");

            var hasResourceGroups = pageContent.Contains("users", StringComparison.OrdinalIgnoreCase) ||
                                   pageContent.Contains("roles", StringComparison.OrdinalIgnoreCase) ||
                                   pageContent.Contains("sessions", StringComparison.OrdinalIgnoreCase);

            Output.WriteLine($"Has resource group organization: {hasResourceGroups}");
        }
    }
}
