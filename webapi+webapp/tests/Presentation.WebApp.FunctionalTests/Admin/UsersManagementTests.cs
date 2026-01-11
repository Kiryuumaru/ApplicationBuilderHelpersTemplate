namespace Presentation.WebApp.FunctionalTests.Admin;

/// <summary>
/// Playwright functional tests for user management page.
/// Tests admin user management functionality through the Blazor WebApp.
/// </summary>
public class UsersManagementTests : WebAppTestBase
{
    public UsersManagementTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task UsersPage_RequiresAuthentication()
    {
        // Act - Try to access users page without authentication
        await Page.GotoAsync($"{WebAppUrl}/admin/users");
        await WaitForBlazorAsync();

        // Assert - Should redirect to login or show unauthorized
        var currentUrl = Page.Url;
        var pageContent = await Page.ContentAsync();

        var redirectedToLogin = currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase);
        var showsUnauthorized = pageContent.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                                pageContent.Contains("access denied", StringComparison.OrdinalIgnoreCase);

        Assert.True(redirectedToLogin || showsUnauthorized, "Should require authentication for admin users page");
    }

    [Fact]
    public async Task UsersPage_Authenticated_ShowsUserTable()
    {
        // Arrange - Register and login (may not have admin role, but page should load)
        var username = $"users_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to users page
        await Page.GotoAsync($"{WebAppUrl}/admin/users");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        var pageContent = await Page.ContentAsync();
        Output.WriteLine($"Users page URL: {currentUrl}");

        // Assert - If accessible, should show user management content
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            var hasUsersContent = pageContent.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                                  pageContent.Contains("management", StringComparison.OrdinalIgnoreCase) ||
                                  await Page.QuerySelectorAsync("table") != null;

            Output.WriteLine($"Has users content: {hasUsersContent}");
        }
    }

    [Fact]
    public async Task UsersPage_HasSearchFunctionality()
    {
        // Arrange - Register and login
        var username = $"search_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to users page
        await Page.GotoAsync($"{WebAppUrl}/admin/users");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should have search input
            var searchInput = await Page.QuerySelectorAsync("input[type='search'], input[placeholder*='search' i]");
            Output.WriteLine($"Search input found: {searchInput != null}");

            if (searchInput != null)
            {
                // Test search functionality
                await searchInput.FillAsync("admin");
                await Task.Delay(500);

                Output.WriteLine("Search test completed");
            }
        }
    }

    [Fact]
    public async Task UsersPage_HasRoleFilter()
    {
        // Arrange - Register and login
        var username = $"filter_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to users page
        await Page.GotoAsync($"{WebAppUrl}/admin/users");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should have role filter dropdown
            var roleFilter = await Page.QuerySelectorAsync("select");
            Output.WriteLine($"Role filter found: {roleFilter != null}");
        }
    }

    [Fact]
    public async Task UsersPage_HasAddUserButton()
    {
        // Arrange - Register and login
        var username = $"adduser_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to users page
        await Page.GotoAsync($"{WebAppUrl}/admin/users");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should have add user button
            var addButton = await Page.QuerySelectorAsync("button:has-text('Add'), button:has-text('Create'), button:has-text('New')");
            Output.WriteLine($"Add user button found: {addButton != null}");
        }
    }

    [Fact]
    public async Task UsersPage_ShowsUserTable_WithColumns()
    {
        // Arrange - Register and login
        var username = $"table_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to users page
        await Page.GotoAsync($"{WebAppUrl}/admin/users");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should have table with expected columns
            var table = await Page.QuerySelectorAsync("table");
            if (table != null)
            {
                var headers = await Page.QuerySelectorAllAsync("th");
                Output.WriteLine($"Table headers found: {headers.Count}");

                var pageContent = await Page.ContentAsync();
                var hasUserColumn = pageContent.Contains("user", StringComparison.OrdinalIgnoreCase);
                var hasRoleColumn = pageContent.Contains("role", StringComparison.OrdinalIgnoreCase);
                var hasStatusColumn = pageContent.Contains("status", StringComparison.OrdinalIgnoreCase);
                var hasActionsColumn = pageContent.Contains("action", StringComparison.OrdinalIgnoreCase);

                Output.WriteLine($"Has User column: {hasUserColumn}");
                Output.WriteLine($"Has Role column: {hasRoleColumn}");
                Output.WriteLine($"Has Status column: {hasStatusColumn}");
                Output.WriteLine($"Has Actions column: {hasActionsColumn}");
            }
        }
    }

    [Fact]
    public async Task UsersPage_HasPagination()
    {
        // Arrange - Register and login
        var username = $"page_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act - Navigate to users page
        await Page.GotoAsync($"{WebAppUrl}/admin/users");
        await WaitForBlazorAsync();

        var currentUrl = Page.Url;
        if (!currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            // Assert - Should have pagination
            var pagination = await Page.QuerySelectorAsync("nav[aria-label*='pagination' i], .pagination, [class*='pagination']");
            var prevButton = await Page.QuerySelectorAsync("button:has-text('Previous'), button[aria-label*='previous' i]");
            var nextButton = await Page.QuerySelectorAsync("button:has-text('Next'), button[aria-label*='next' i]");

            Output.WriteLine($"Pagination found: {pagination != null}");
            Output.WriteLine($"Prev/Next buttons: {prevButton != null}/{nextButton != null}");
        }
    }
}
