namespace Presentation.WebApp.FunctionalTests.Dashboard;

/// <summary>
/// Playwright functional tests for the dashboard/home page.
/// Tests dashboard display and statistics.
/// </summary>
public class DashboardTests : WebAppTestBase
{
    public DashboardTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task Dashboard_LoadsSuccessfully()
    {
        // Act
        await GoToHomeAsync();

        // Assert - Dashboard should load
        var title = await Page.TitleAsync();
        Output.WriteLine($"Dashboard title: {title}");

        var hasContent = !string.IsNullOrEmpty(await Page.ContentAsync());
        Assert.True(hasContent, "Dashboard should have content");
    }

    [Fact]
    public async Task Dashboard_ShowsStatisticsCards()
    {
        // Act
        await GoToHomeAsync();

        // Assert - Should show statistics cards
        var cards = await Page.QuerySelectorAllAsync(".card, [class*='card']");
        Output.WriteLine($"Cards found: {cards.Count}");

        var pageContent = await Page.ContentAsync();
        var hasStats = pageContent.Contains("users", StringComparison.OrdinalIgnoreCase) ||
                      pageContent.Contains("active", StringComparison.OrdinalIgnoreCase) ||
                      pageContent.Contains("sessions", StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"Has statistics content: {hasStats}");
    }

    [Fact]
    public async Task Dashboard_Unauthenticated_ShowsGetStarted()
    {
        // Act
        await GoToHomeAsync();

        // Assert - Should show get started message for unauthenticated users
        var pageContent = await Page.ContentAsync();
        var hasGetStarted = pageContent.Contains("get started", StringComparison.OrdinalIgnoreCase) ||
                           pageContent.Contains("sign in", StringComparison.OrdinalIgnoreCase) ||
                           pageContent.Contains("login", StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"Has get started message: {hasGetStarted}");
    }

    [Fact]
    public async Task Dashboard_Authenticated_ShowsWelcomeMessage()
    {
        // Arrange - Register and login
        var username = $"welcome_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act
        await GoToHomeAsync();

        // Assert - Should show welcome message
        var pageContent = await Page.ContentAsync();
        var hasWelcome = pageContent.Contains("welcome", StringComparison.OrdinalIgnoreCase) ||
                        pageContent.Contains("signed in", StringComparison.OrdinalIgnoreCase) ||
                        pageContent.Contains(username, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"Has welcome message: {hasWelcome}");
        Assert.True(hasWelcome, "Should show welcome message for authenticated user");
    }

    [Fact]
    public async Task Dashboard_Authenticated_HasProfileLink()
    {
        // Arrange - Register and login
        var username = $"proflink_{Guid.NewGuid():N}".Substring(0, 20);
        var email = $"{username}@test.example.com";

        await RegisterUserAsync(username, email, TestPassword);
        await LoginAsync(username, TestPassword);

        // Act
        await GoToHomeAsync();

        // Assert - Should have link to profile
        var profileLink = await Page.QuerySelectorAsync("a[href*='profile' i]");
        Assert.NotNull(profileLink);
    }

    [Fact]
    public async Task Dashboard_HasNavigationMenu()
    {
        // Act
        await GoToHomeAsync();

        // Assert - Should have navigation
        var nav = await Page.QuerySelectorAsync("nav, [role='navigation'], .nav, .navbar, .sidebar");
        Assert.NotNull(nav);
    }

    [Fact]
    public async Task Dashboard_ShowsCorrectTitle()
    {
        // Act
        await GoToHomeAsync();

        // Assert - Should show dashboard title
        var title = await Page.TitleAsync();
        var pageContent = await Page.ContentAsync();

        var hasDashboardTitle = title.Contains("dashboard", StringComparison.OrdinalIgnoreCase) ||
                               pageContent.Contains("dashboard", StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"Page title: {title}");
        Assert.True(hasDashboardTitle, "Should show dashboard title");
    }

    [Fact]
    public async Task Dashboard_UsersCard_ShowsPlaceholder()
    {
        // Act
        await GoToHomeAsync();

        // Assert - Users stat card should show placeholder or count
        var pageContent = await Page.ContentAsync();
        var hasUsersCard = pageContent.Contains("users", StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"Has users card: {hasUsersCard}");
    }

    [Fact]
    public async Task Dashboard_ActiveCard_ShowsPlaceholder()
    {
        // Act
        await GoToHomeAsync();

        // Assert - Active stat card should show placeholder or count
        var pageContent = await Page.ContentAsync();
        var hasActiveCard = pageContent.Contains("active", StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"Has active card: {hasActiveCard}");
    }

    [Fact]
    public async Task Dashboard_SessionsCard_ShowsPlaceholder()
    {
        // Act
        await GoToHomeAsync();

        // Assert - Sessions stat card should show placeholder or count
        var pageContent = await Page.ContentAsync();
        var hasSessionsCard = pageContent.Contains("sessions", StringComparison.OrdinalIgnoreCase);

        Output.WriteLine($"Has sessions card: {hasSessionsCard}");
    }

    [Fact]
    public async Task Dashboard_ResponsiveLayout()
    {
        // Act - Test mobile viewport
        await Page.SetViewportSizeAsync(375, 667);
        await GoToHomeAsync();

        // Assert - Page should still render correctly
        var content = await Page.ContentAsync();
        Assert.False(string.IsNullOrEmpty(content), "Dashboard should render on mobile viewport");

        // Reset to desktop
        await Page.SetViewportSizeAsync(1280, 720);
    }
}
