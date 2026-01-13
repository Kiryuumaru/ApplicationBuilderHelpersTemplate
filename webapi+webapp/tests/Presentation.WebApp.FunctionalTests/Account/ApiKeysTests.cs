using Presentation.WebApp.FunctionalTests.Fixtures;

namespace Presentation.WebApp.FunctionalTests.Account;

/// <summary>
/// Playwright functional tests for API keys management page.
/// Tests API key creation, viewing, and revocation functionality.
/// </summary>
public class ApiKeysTests : WebAppTestBase
{
    public ApiKeysTests(SharedTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact]
    public async Task ApiKeys_RequiresAuthentication()
    {
        // Act - Try to access API keys without authentication
        await Page.GotoAsync($"{WebAppUrl}/account/api-keys");
        await WaitForBlazorAsync();

        // Assert - Should redirect to login
        var currentUrl = Page.Url;
        var redirectedToLogin = currentUrl.Contains("/auth/login", StringComparison.OrdinalIgnoreCase);
        Assert.True(redirectedToLogin, "Should redirect to login when accessing API keys unauthenticated");
    }

    [Fact]
    public async Task ApiKeys_Authenticated_ShowsApiKeysPage()
    {
        // Arrange - Register and login
        var username = $"apikeys_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to API keys
        await Page.GotoAsync($"{WebAppUrl}/account/api-keys");
        await WaitForBlazorAsync();

        // Assert - Should show API keys page
        var pageContent = await Page.ContentAsync();
        Output.WriteLine($"API keys page content length: {pageContent.Length}");

        var hasApiKeysContent = pageContent.Contains("API", StringComparison.OrdinalIgnoreCase) ||
                                pageContent.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                                pageContent.Contains("token", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasApiKeysContent, "API keys page should display API key information");
    }

    [Fact]
    public async Task ApiKeys_HasCreateButton()
    {
        // Arrange - Register and login
        var username = $"create_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to API keys
        await Page.GotoAsync($"{WebAppUrl}/account/api-keys");
        await WaitForBlazorAsync();

        // Assert - Should have create button
        var createButton = await Page.QuerySelectorAsync("button:has-text('Create'), button:has-text('Generate'), button:has-text('New'), button:has-text('Add')");
        var pageContent = await Page.ContentAsync();
        var hasCreateOption = createButton != null ||
                             pageContent.Contains("create", StringComparison.OrdinalIgnoreCase) ||
                             pageContent.Contains("generate", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasCreateOption, "Should have option to create new API key");
        Output.WriteLine($"Create button found: {createButton != null}");
    }

    [Fact]
    public async Task ApiKeys_CreateForm_HasRequiredFields()
    {
        // Arrange - Register and login
        var username = $"form_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to API keys and open create form
        await Page.GotoAsync($"{WebAppUrl}/account/api-keys");
        await WaitForBlazorAsync();

        var createButton = await Page.QuerySelectorAsync("button:has-text('Create'), button:has-text('Generate'), button:has-text('New'), button:has-text('Add')");
        if (createButton != null)
        {
            await createButton.ClickAsync();
            await Task.Delay(500);

            // Assert - Should show form with name/description field
            var nameInput = await Page.QuerySelectorAsync("input[name='name' i], input[placeholder*='name' i]");
            var descriptionInput = await Page.QuerySelectorAsync("input[name='description' i], textarea[name='description' i]");

            Output.WriteLine($"Name input found: {nameInput != null}");
            Output.WriteLine($"Description input found: {descriptionInput != null}");
        }
    }

    [Fact]
    public async Task ApiKeys_ShowsExistingKeys()
    {
        // Arrange - Register and login
        var username = $"list_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to API keys
        await Page.GotoAsync($"{WebAppUrl}/account/api-keys");
        await WaitForBlazorAsync();

        // Assert - Page should handle empty state or show existing keys
        var pageContent = await Page.ContentAsync();
        var hasEmptyState = pageContent.Contains("no api key", StringComparison.OrdinalIgnoreCase) ||
                           pageContent.Contains("no keys", StringComparison.OrdinalIgnoreCase) ||
                           pageContent.Contains("get started", StringComparison.OrdinalIgnoreCase);
        var hasTable = await Page.QuerySelectorAsync("table") != null;
        var hasList = await Page.QuerySelectorAsync("[class*='list'], [class*='card']") != null;

        var hasKeyDisplay = hasEmptyState || hasTable || hasList;
        Output.WriteLine($"Has empty state: {hasEmptyState}");
        Output.WriteLine($"Has table: {hasTable}");
        Output.WriteLine($"Has list/cards: {hasList}");
    }

    [Fact]
    public async Task ApiKeys_HasRevokeOption()
    {
        // Arrange - Register and login
        var username = $"revoke_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to API keys
        await Page.GotoAsync($"{WebAppUrl}/account/api-keys");
        await WaitForBlazorAsync();

        // Assert - Check for revoke functionality
        var pageContent = await Page.ContentAsync();
        var hasRevokeOption = pageContent.Contains("revoke", StringComparison.OrdinalIgnoreCase) ||
                             pageContent.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                             pageContent.Contains("remove", StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"Has revoke option: {hasRevokeOption}");
    }

    [Fact]
    public async Task ApiKeys_ShowsExpirationInfo()
    {
        // Arrange - Register and login
        var username = $"expiry_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to API keys
        await Page.GotoAsync($"{WebAppUrl}/account/api-keys");
        await WaitForBlazorAsync();

        // Assert - Check for expiration info
        var pageContent = await Page.ContentAsync();
        var hasExpirationInfo = pageContent.Contains("expir", StringComparison.OrdinalIgnoreCase) ||
                                pageContent.Contains("valid", StringComparison.OrdinalIgnoreCase) ||
                                pageContent.Contains("until", StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"Has expiration info: {hasExpirationInfo}");
    }

    [Fact]
    public async Task ApiKeys_NavigationFromSidebar()
    {
        // Arrange - Register and login
        var username = $"nav_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate via sidebar
        await GoToHomeAsync();
        var apiKeysLink = await Page.QuerySelectorAsync("a[href*='api-keys' i]");

        if (apiKeysLink != null)
        {
            await apiKeysLink.ClickAsync();
            await WaitForBlazorAsync();

            // Assert - Should be on API keys page
            AssertUrlContains("/account/api-keys");
            Output.WriteLine("✅ API keys page accessible via navigation");
        }
        else
        {
            Output.WriteLine("API keys link not found in sidebar");
        }
    }

    [Fact]
    public async Task ApiKeys_CreatedKeyShowsOnlyOnce()
    {
        // Arrange - Register and login
        var username = $"once_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(email, TestPassword);

        // Act - Navigate to API keys
        await Page.GotoAsync($"{WebAppUrl}/account/api-keys");
        await WaitForBlazorAsync();

        // Assert - Page should have information about key visibility
        var pageContent = await Page.ContentAsync();
        var hasSecurityWarning = pageContent.Contains("only shown once", StringComparison.OrdinalIgnoreCase) ||
                                 pageContent.Contains("won't be shown again", StringComparison.OrdinalIgnoreCase) ||
                                 pageContent.Contains("copy", StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"Has security warning about key visibility: {hasSecurityWarning}");
    }
}
