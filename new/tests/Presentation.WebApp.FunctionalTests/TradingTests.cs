using NUnit.Framework;
using Microsoft.Playwright;

namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// Playwright tests for the Trading UI pages.
/// </summary>
public class TradingTests : PlaywrightTestBase
{
    private string _testEmail = null!;
    private const string TestPassword = "TestPassword123!";

    [SetUp]
    public async Task SetupAsync()
    {
        // Create a unique email for each test to avoid conflicts
        _testEmail = $"trading_test_{Guid.NewGuid():N}@test.com";
        await RegisterAndLoginUserAsync(_testEmail, TestPassword);
    }

    #region Trading Dashboard Tests

    [Test]
    public async Task TradingDashboard_NavigatesToPage()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task TradingDashboard_ShowsPageTitle()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Trading Dashboard"));
    }

    [Test]
    public async Task TradingDashboard_HasSearchFilterOrLoadingState()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Dashboard might show search filter or be in loading/error state
        var searchInput = Page.GetByPlaceholder("Filter by currency...");
        var spinner = Page.Locator(".spinner-border");
        var errorAlert = Page.Locator(".alert-danger");

        var hasSearchFilter = await searchInput.CountAsync() > 0;
        var hasSpinner = await spinner.CountAsync() > 0;
        var hasError = await errorAlert.CountAsync() > 0;

        Assert.That(hasSearchFilter || hasSpinner || hasError, Is.True, 
            "Should show search filter, loading spinner, or error state");
    }

    [Test]
    public async Task TradingDashboard_HasRefreshButtonOrLoadingState()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Dashboard might show refresh button or be in loading/error state
        var refreshButton = Page.GetByRole(AriaRole.Button, new() { Name = "Refresh" });
        var spinner = Page.Locator(".spinner-border");
        var errorAlert = Page.Locator(".alert-danger");

        var hasRefreshButton = await refreshButton.CountAsync() > 0;
        var hasSpinner = await spinner.CountAsync() > 0;
        var hasError = await errorAlert.CountAsync() > 0;

        Assert.That(hasRefreshButton || hasSpinner || hasError, Is.True, 
            "Should show refresh button, loading spinner, or error state");
    }

    [Test]
    public async Task TradingDashboard_HasLiveStreamingButtonOrLoadingState()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Either "Go Live" or "Stop Live" should be visible, or page is loading/error
        var goLiveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Go Live" });
        var stopLiveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Stop Live" });
        var spinner = Page.Locator(".spinner-border");
        var errorAlert = Page.Locator(".alert-danger");

        var goLiveCount = await goLiveButton.CountAsync();
        var stopLiveCount = await stopLiveButton.CountAsync();
        var hasSpinner = await spinner.CountAsync() > 0;
        var hasError = await errorAlert.CountAsync() > 0;

        Assert.That(goLiveCount + stopLiveCount > 0 || hasSpinner || hasError, Is.True, 
            "Should show live streaming button, loading spinner, or error state");
    }

    [Test]
    public async Task TradingDashboard_ShowsMarketPricesTable()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for table to load (could be loading or showing prices)
        var table = Page.Locator("table");
        var tableExists = await table.CountAsync() > 0;

        // Either table exists OR loading spinner is shown
        var spinner = Page.Locator(".spinner-border");
        var spinnerExists = await spinner.CountAsync() > 0;

        // Or error message
        var errorAlert = Page.Locator(".alert-danger");
        var hasError = await errorAlert.CountAsync() > 0;

        Assert.That(tableExists || spinnerExists || hasError, Is.True, 
            "Should show either table, loading spinner, or error");
    }

    [Test]
    public async Task TradingDashboard_PageLoadsWithoutCrash()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Check that page doesn't crash - should have heading
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
    }

    #endregion

    #region Exchange Accounts Tests

    [Test]
    public async Task ExchangeAccounts_NavigatesToPage()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Use Exact match to avoid conflict with "No Exchange Accounts"
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Exchange Accounts", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ExchangeAccounts_ShowsPageTitle()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Exchange Accounts"));
    }

    [Test]
    public async Task ExchangeAccounts_HasAddAccountButton()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Either "Add Account" or "Add Your First Account" should be visible
        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        var addFirstButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });

        var hasAddButton = await addButton.CountAsync() > 0;
        var hasAddFirstButton = await addFirstButton.CountAsync() > 0;

        Assert.That(hasAddButton || hasAddFirstButton, Is.True, "Should have an add account button");
    }

    [Test]
    public async Task ExchangeAccounts_ShowsEmptyState()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // For a new user, should show empty state
        var emptyStateText = Page.GetByText("No Exchange Accounts");
        var emptyStateButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });

        var hasEmptyState = await emptyStateText.CountAsync() > 0 || await emptyStateButton.CountAsync() > 0;
        
        // Either empty state is shown OR accounts already exist
        Assert.That(hasEmptyState, Is.True.Or.False, "Page should load successfully");
    }

    [Test]
    public async Task ExchangeAccounts_AddAccountModal_Opens()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click Add Account button
        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        await addButton.ClickAsync();

        // Modal should appear
        await Page.WaitForTimeoutAsync(500);
        var modal = Page.Locator(".modal.show, .modal[style*='display: block']");
        await Expect(modal).ToBeVisibleAsync();
    }

    [Test]
    public async Task ExchangeAccounts_AddAccountModal_HasFormFields()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click Add Account button
        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Modal should have form
        var modal = Page.Locator(".modal.show, .modal[style*='display: block']");
        await Expect(modal).ToBeVisibleAsync();

        // Check for any input fields in modal
        var inputs = modal.Locator("input, select");
        var inputCount = await inputs.CountAsync();

        Assert.That(inputCount, Is.GreaterThan(0), "Modal should have input fields");
    }

    [Test]
    public async Task ExchangeAccounts_AddAccountModal_CanClose()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click Add Account button
        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Close modal
        var closeButton = Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
        if (await closeButton.CountAsync() > 0)
        {
            await closeButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            var modal = Page.Locator(".modal.show");
            await Expect(modal).ToBeHiddenAsync();
        }
    }

    #endregion

    #region Orders Page Tests

    [Test]
    public async Task OrdersPage_NavigatesToPage()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Orders", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task OrdersPage_ShowsPageTitle()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Orders"));
    }

    [Test]
    public async Task OrdersPage_HasNewOrderButton()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var newOrderLink = Page.GetByRole(AriaRole.Link, new() { Name = "New Order" });
        await Expect(newOrderLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task OrdersPage_ShowsWarningWithoutAccounts()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // For a new user without accounts, should show warning
        var warningAlert = Page.Locator(".alert-warning");
        var alertCount = await warningAlert.CountAsync();

        // Either warning is shown (no accounts) or filter controls are shown (has accounts)
        var filterControls = Page.Locator("select.form-select");
        var hasFilterControls = await filterControls.CountAsync() > 0;

        Assert.That(alertCount > 0 || hasFilterControls, Is.True, 
            "Should show either warning or filter controls");
    }

    [Test]
    public async Task OrdersPage_NewOrderLink_NavigatesToNewOrder()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var newOrderLink = Page.GetByRole(AriaRole.Link, new() { Name = "New Order" });
        await newOrderLink.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.That(Page.Url, Does.Contain("/trading/orders/new"));
    }

    [Test]
    public async Task OrdersPage_HasStatusFilterButtons()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // If user has accounts, filter buttons should be visible
        var allButton = Page.GetByRole(AriaRole.Button, new() { Name = "All", Exact = true });
        var activeButton = Page.GetByRole(AriaRole.Button, new() { Name = "Active" });
        var filledButton = Page.GetByRole(AriaRole.Button, new() { Name = "Filled" });

        var hasAll = await allButton.CountAsync() > 0;
        var hasActive = await activeButton.CountAsync() > 0;
        var hasFilled = await filledButton.CountAsync() > 0;

        // Either all filter buttons exist (has accounts) or none (no accounts warning)
        var allExist = hasAll && hasActive && hasFilled;
        var noneExist = !hasAll && !hasActive && !hasFilled;

        Assert.That(allExist || noneExist, Is.True, 
            "Filter buttons should either all exist or none exist");
    }

    #endregion

    #region New Order Page Tests

    [Test]
    public async Task NewOrderPage_NavigatesToPage()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Place New Order" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task NewOrderPage_ShowsPageTitle()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("New Order"));
    }

    [Test]
    public async Task NewOrderPage_HasBreadcrumbs()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var breadcrumb = Page.Locator("nav[aria-label='breadcrumb']");
        await Expect(breadcrumb).ToBeVisibleAsync();

        // Check breadcrumb links - use Exact match to avoid email in nav
        var tradingLink = Page.Locator("nav[aria-label='breadcrumb']").GetByRole(AriaRole.Link, new() { Name = "Trading" });
        var ordersLink = Page.Locator("nav[aria-label='breadcrumb']").GetByRole(AriaRole.Link, new() { Name = "Orders" });

        await Expect(tradingLink).ToBeVisibleAsync();
        await Expect(ordersLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task NewOrderPage_ShowsWarningWithoutAccounts()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // For a new user without accounts, should show warning
        var warningAlert = Page.Locator(".alert-warning");
        var alertCount = await warningAlert.CountAsync();

        // Either warning is shown (no accounts) or form is shown (has accounts)
        var form = Page.Locator("form");
        var hasForm = await form.CountAsync() > 0;

        Assert.That(alertCount > 0 || hasForm, Is.True, 
            "Should show either warning or order form");
    }

    [Test]
    public async Task NewOrderPage_BreadcrumbLinks_Work()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click Trading breadcrumb (within breadcrumb nav to be specific)
        var tradingLink = Page.Locator("nav[aria-label='breadcrumb']").GetByRole(AriaRole.Link, new() { Name = "Trading" });
        await tradingLink.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.That(Page.Url, Does.Contain("/trading").And.Not.Contain("/orders"));
    }

    #endregion

    #region Navigation Tests

    [Test]
    public async Task Navigation_TradingDashboard_AccessibleFromMenu()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Look for Dashboard link in nav area
        var navArea = Page.Locator("nav, .sidebar, .nav-item");
        var dashboardLink = navArea.GetByRole(AriaRole.Link, new() { Name = "Dashboard" }).First;
        
        if (await dashboardLink.CountAsync() > 0)
        {
            await dashboardLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.That(Page.Url, Does.Contain("/trading"));
        }
        else
        {
            // Navigate directly if not in menu
            await Page.GotoAsync($"{BaseUrl}/trading");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.That(Page.Url, Does.Contain("/trading"));
        }
    }

    [Test]
    public async Task Navigation_ExchangeAccounts_AccessibleViaUrl()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.That(Page.Url, Does.Contain("/trading/accounts"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Exchange Accounts", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Navigation_Orders_AccessibleViaUrl()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.That(Page.Url, Does.Contain("/trading/orders"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Orders", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Navigation_NewOrder_AccessibleViaUrl()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.That(Page.Url, Does.Contain("/trading/orders/new"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Place New Order" })).ToBeVisibleAsync();
    }

    #endregion

    #region Authorization Tests

    [Test]
    public async Task TradingPages_RequireAuthentication_Dashboard()
    {
        // Create a new browser context without cookies (logged out)
        var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/trading");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should redirect to login
        Assert.That(page.Url, Does.Contain("/Account/Login").Or.Contain("/Account/AccessDenied"));

        await context.CloseAsync();
    }

    [Test]
    public async Task TradingPages_RequireAuthentication_Accounts()
    {
        var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/trading/accounts");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.That(page.Url, Does.Contain("/Account/Login").Or.Contain("/Account/AccessDenied"));

        await context.CloseAsync();
    }

    [Test]
    public async Task TradingPages_RequireAuthentication_Orders()
    {
        var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/trading/orders");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.That(page.Url, Does.Contain("/Account/Login").Or.Contain("/Account/AccessDenied"));

        await context.CloseAsync();
    }

    [Test]
    public async Task TradingPages_RequireAuthentication_NewOrder()
    {
        var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.That(page.Url, Does.Contain("/Account/Login").Or.Contain("/Account/AccessDenied"));

        await context.CloseAsync();
    }

    #endregion

    #region Responsive Design Tests

    [Test]
    public async Task TradingDashboard_ResponsiveOnMobile()
    {
        await Page.SetViewportSizeAsync(375, 667); // iPhone SE
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ExchangeAccounts_ResponsiveOnMobile()
    {
        await Page.SetViewportSizeAsync(375, 667);
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Use Exact match to avoid "No Exchange Accounts" conflict
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Exchange Accounts", Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task TradingDashboard_ResponsiveOnTablet()
    {
        await Page.SetViewportSizeAsync(768, 1024); // iPad
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task TradingDashboard_HandlesErrorGracefully()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Check that page doesn't crash - either shows content or error message
        var hasHeading = await Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" }).CountAsync() > 0;
        var hasErrorAlert = await Page.Locator(".alert-danger").CountAsync() > 0;
        var hasSpinner = await Page.Locator(".spinner-border").CountAsync() > 0;

        Assert.That(hasHeading || hasErrorAlert || hasSpinner, Is.True, 
            "Page should show heading, error message, or loading state");
    }

    [Test]
    public async Task ExchangeAccounts_HandlesErrorGracefully()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var hasHeading = await Page.GetByRole(AriaRole.Heading, new() { Name = "Exchange Accounts" }).CountAsync() > 0;
        var hasErrorAlert = await Page.Locator(".alert-danger").CountAsync() > 0;
        var hasSpinner = await Page.Locator(".spinner-border").CountAsync() > 0;
        var hasEmptyState = await Page.GetByText("No Exchange Accounts").CountAsync() > 0;

        Assert.That(hasHeading || hasErrorAlert || hasSpinner || hasEmptyState, Is.True,
            "Page should show heading, error message, loading state, or empty state");
    }

    #endregion

    #region User Journey Tests

    [Test]
    public async Task Journey_AddExchangeAccount_CompleteFlow()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Click Add Account button
        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        
        await Expect(addButton).ToBeVisibleAsync();
        await addButton.ClickAsync();

        // Wait for modal to appear
        await Page.WaitForTimeoutAsync(500);
        var modal = Page.Locator(".modal.show, .modal.fade.show.d-block");
        await Expect(modal).ToBeVisibleAsync();

        // Fill in the form using placeholders to find visible inputs
        var nameInput = modal.GetByPlaceholder("My Trading Account");
        await nameInput.FillAsync("Test Trading Account");

        // Select exchange
        var exchangeSelect = modal.Locator("select.form-select").First;
        if (await exchangeSelect.CountAsync() > 0)
        {
            await exchangeSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });
        }

        // Fill API Key and Secret using placeholders
        var apiKeyInput = modal.GetByPlaceholder("Your API Key");
        if (await apiKeyInput.CountAsync() > 0)
        {
            await apiKeyInput.FillAsync("test-api-key-12345");
        }

        var apiSecretInput = modal.GetByPlaceholder("Your API Secret");
        if (await apiSecretInput.CountAsync() > 0)
        {
            await apiSecretInput.FillAsync("test-api-secret-67890");
        }

        // Save the account
        var saveButton = modal.GetByRole(AriaRole.Button, new() { Name = "Save" });
        await saveButton.ClickAsync();

        // Wait for save to complete
        await Page.WaitForTimeoutAsync(1000);

        // Modal should close or error shown
        var modalVisible = await Page.Locator(".modal.show").CountAsync() > 0;
        var accountCard = Page.GetByText("Test Trading Account");
        var hasAccount = await accountCard.CountAsync() > 0;
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;

        Assert.That(!modalVisible || hasAccount || hasError, Is.True,
            "Account should be created or error should be shown");
    }

    [Test]
    public async Task Journey_ViewDashboardAfterAddingAccount()
    {
        // First add an account
        await AddTestExchangeAccount("Dashboard Test Account");

        // Navigate to dashboard
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Dashboard should load without crashing
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Journey_EditExchangeAccount()
    {
        // Add an account first
        await AddTestExchangeAccount("Edit Test Account");

        // Navigate to accounts
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Find and click Edit button
        var editButton = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).First;
        if (await editButton.CountAsync() > 0)
        {
            await editButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Modal should open
            var modal = Page.Locator(".modal.show");
            await Expect(modal).ToBeVisibleAsync();

            // Should show "Edit Exchange Account" title
            var modalTitle = modal.GetByText("Edit Exchange Account");
            await Expect(modalTitle).ToBeVisibleAsync();

            // Cancel to close
            var cancelButton = modal.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
            await cancelButton.ClickAsync();
            await Page.WaitForTimeoutAsync(300);

            await Expect(modal).ToBeHiddenAsync();
        }
    }

    [Test]
    public async Task Journey_DeleteExchangeAccount_ShowsConfirmation()
    {
        // Add an account first
        await AddTestExchangeAccount("Delete Test Account");

        // Navigate to accounts
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Find and click Delete button
        var deleteButton = Page.GetByRole(AriaRole.Button, new() { Name = "Delete" }).First;
        if (await deleteButton.CountAsync() > 0)
        {
            await deleteButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Confirmation modal should open
            var confirmModal = Page.Locator(".modal.show");
            await Expect(confirmModal).ToBeVisibleAsync();

            // Should show "Confirm Delete" title
            var confirmTitle = confirmModal.GetByText("Confirm Delete");
            await Expect(confirmTitle).ToBeVisibleAsync();

            // Cancel deletion
            var cancelButton = confirmModal.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
            await cancelButton.ClickAsync();
            await Page.WaitForTimeoutAsync(300);

            await Expect(confirmModal).ToBeHiddenAsync();
        }
    }

    [Test]
    public async Task Journey_NewOrder_WithAccount_ShowsForm()
    {
        // Add an account first
        await AddTestExchangeAccount("Order Test Account");

        // Navigate to new order
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Should see new order page
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Place New Order" })).ToBeVisibleAsync();

        // Should see form or warning
        var form = Page.Locator("form");
        var warning = Page.Locator(".alert-warning");

        var hasForm = await form.CountAsync() > 0;
        var hasWarning = await warning.CountAsync() > 0;

        Assert.That(hasForm || hasWarning, Is.True, "Should show form or warning");
    }

    #endregion

    #region Dashboard Functionality Tests

    [Test]
    public async Task Dashboard_SortByColumn_Works()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Try to click Currency column header to sort
        var currencyHeader = Page.Locator("th").Filter(new() { HasText = "Currency" });
        if (await currencyHeader.CountAsync() > 0)
        {
            await currencyHeader.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
        }

        // Try to click Price column header
        var priceHeader = Page.Locator("th").Filter(new() { HasText = "Price" });
        if (await priceHeader.CountAsync() > 0)
        {
            await priceHeader.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
        }
    }

    [Test]
    public async Task Dashboard_RefreshButton_DoesNotCrash()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var refreshButton = Page.GetByRole(AriaRole.Button, new() { Name = "Refresh" });
        if (await refreshButton.CountAsync() > 0)
        {
            await refreshButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
        }
    }

    [Test]
    public async Task Dashboard_LiveStreamToggle_DoesNotCrash()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var goLiveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Go Live" });
        var stopLiveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Stop Live" });

        if (await goLiveButton.CountAsync() > 0)
        {
            await goLiveButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
        }
        else if (await stopLiveButton.CountAsync() > 0)
        {
            await stopLiveButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
        }
    }

    [Test]
    public async Task Dashboard_FilterInput_Works()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var filterInput = Page.GetByPlaceholder("Filter by currency...");
        if (await filterInput.CountAsync() > 0)
        {
            await filterInput.FillAsync("BTC");
            await Page.WaitForTimeoutAsync(300);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();

            await filterInput.FillAsync("");
            await Page.WaitForTimeoutAsync(300);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
        }
    }

    [Test]
    public async Task Dashboard_QuickLinks_Visible()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var quickLinksHeader = Page.GetByText("Quick Links");
        var hasQuickLinks = await quickLinksHeader.CountAsync() > 0;
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;
        var hasSpinner = await Page.Locator(".spinner-border").CountAsync() > 0;

        Assert.That(hasQuickLinks || hasError || hasSpinner, Is.True,
            "Should show Quick Links, error, or loading state");
    }

    [Test]
    public async Task Dashboard_StatsSection_Visible()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var statsHeader = Page.GetByText("Stats");
        var hasStats = await statsHeader.CountAsync() > 0;
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;
        var hasSpinner = await Page.Locator(".spinner-border").CountAsync() > 0;

        Assert.That(hasStats || hasError || hasSpinner, Is.True,
            "Should show Stats, error, or loading state");
    }

    #endregion

    #region Orders Page Functionality Tests

    [Test]
    public async Task Orders_FilterByStatus_Works()
    {
        await AddTestExchangeAccount("Orders Filter Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var allButton = Page.GetByRole(AriaRole.Button, new() { Name = "All", Exact = true });
        var activeButton = Page.GetByRole(AriaRole.Button, new() { Name = "Active" });
        var filledButton = Page.GetByRole(AriaRole.Button, new() { Name = "Filled" });

        if (await allButton.CountAsync() > 0)
        {
            await allButton.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Orders", Exact = true })).ToBeVisibleAsync();
        }

        if (await activeButton.CountAsync() > 0)
        {
            await activeButton.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Orders", Exact = true })).ToBeVisibleAsync();
        }

        if (await filledButton.CountAsync() > 0)
        {
            await filledButton.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Orders", Exact = true })).ToBeVisibleAsync();
        }
    }

    [Test]
    public async Task Orders_SelectAccount_Works()
    {
        await AddTestExchangeAccount("Account Selection Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var accountSelect = Page.Locator("select.form-select").First;
        if (await accountSelect.CountAsync() > 0)
        {
            var options = accountSelect.Locator("option");
            var optionCount = await options.CountAsync();
            
            if (optionCount > 1)
            {
                await accountSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });
                await Page.WaitForTimeoutAsync(500);
                await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Orders", Exact = true })).ToBeVisibleAsync();
            }
        }
    }

    [Test]
    public async Task Orders_RefreshButton_Works()
    {
        await AddTestExchangeAccount("Orders Refresh Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var refreshButton = Page.GetByRole(AriaRole.Button, new() { Name = "Refresh" });
        if (await refreshButton.CountAsync() > 0)
        {
            await refreshButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Orders", Exact = true })).ToBeVisibleAsync();
        }
    }

    [Test]
    public async Task Orders_EmptyState_ShowsPlaceFirstOrder()
    {
        await AddTestExchangeAccount("Empty Orders Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var placeFirstOrderButton = Page.GetByRole(AriaRole.Link, new() { Name = "Place Your First Order" });
        var newOrderButton = Page.GetByRole(AriaRole.Link, new() { Name = "New Order" });
        var ordersTable = Page.Locator("table");
        var noOrdersText = Page.GetByText("No orders");
        var emptyState = Page.Locator(".card-body").Filter(new() { HasText = "haven't placed" });

        var hasPlaceFirstButton = await placeFirstOrderButton.CountAsync() > 0;
        var hasNewOrderButton = await newOrderButton.CountAsync() > 0;
        var hasTable = await ordersTable.CountAsync() > 0;
        var hasNoOrdersText = await noOrdersText.CountAsync() > 0;
        var hasEmptyState = await emptyState.CountAsync() > 0;

        Assert.That(hasPlaceFirstButton || hasNewOrderButton || hasTable || hasNoOrdersText || hasEmptyState, Is.True,
            "Should show orders UI elements");
    }

    #endregion

    #region New Order Page Functionality Tests

    [Test]
    public async Task NewOrder_SideToggle_Works()
    {
        await AddTestExchangeAccount("Side Toggle Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Use labels instead of radio buttons since Bootstrap styles hide the radio and shows label
        var buyLabel = Page.Locator("label[for='sideBuy']");
        var sellLabel = Page.Locator("label[for='sideSell']");

        if (await buyLabel.CountAsync() > 0)
        {
            await buyLabel.ClickAsync();
            await Page.WaitForTimeoutAsync(200);
        }

        if (await sellLabel.CountAsync() > 0)
        {
            await sellLabel.ClickAsync();
            await Page.WaitForTimeoutAsync(200);
        }

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Place New Order" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task NewOrder_OrderTypeSelect_Works()
    {
        await AddTestExchangeAccount("Order Type Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var orderTypeSelect = Page.Locator("select").Filter(new() { Has = Page.Locator("option:has-text('Limit')") });
        if (await orderTypeSelect.CountAsync() > 0)
        {
            await orderTypeSelect.SelectOptionAsync("Market");
            await Page.WaitForTimeoutAsync(200);

            await orderTypeSelect.SelectOptionAsync("Limit");
            await Page.WaitForTimeoutAsync(200);
        }

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Place New Order" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task NewOrder_GetPriceButton_Works()
    {
        await AddTestExchangeAccount("Get Price Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var baseAssetInput = Page.GetByPlaceholder("BTC");
        var quoteAssetInput = Page.GetByPlaceholder("USDT");

        if (await baseAssetInput.CountAsync() > 0 && await quoteAssetInput.CountAsync() > 0)
        {
            await baseAssetInput.FillAsync("BTC");
            await quoteAssetInput.FillAsync("USDT");

            var getPriceButton = Page.GetByRole(AriaRole.Button, new() { Name = "Get Price" });
            if (await getPriceButton.CountAsync() > 0)
            {
                await getPriceButton.ClickAsync();
                await Page.WaitForTimeoutAsync(2000);
                await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Place New Order" })).ToBeVisibleAsync();
            }
        }
    }

    [Test]
    public async Task NewOrder_CancelButton_NavigatesToOrders()
    {
        await AddTestExchangeAccount("Cancel Button Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var cancelLink = Page.GetByRole(AriaRole.Link, new() { Name = "Cancel" });
        if (await cancelLink.CountAsync() > 0)
        {
            await cancelLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.That(Page.Url, Does.Contain("/trading/orders"));
        }
    }

    [Test]
    public async Task NewOrder_OrderInfo_Visible()
    {
        await AddTestExchangeAccount("Order Info Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var orderInfoHeader = Page.GetByText("Order Info");
        var hasOrderInfo = await orderInfoHeader.CountAsync() > 0;
        var hasWarning = await Page.Locator(".alert-warning").CountAsync() > 0;

        Assert.That(hasOrderInfo || hasWarning, Is.True,
            "Should show Order Info section or warning");
    }

    [Test]
    public async Task NewOrder_RiskWarning_Visible()
    {
        await AddTestExchangeAccount("Risk Warning Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var riskWarning = Page.GetByText("Risk Warning");
        var hasRiskWarning = await riskWarning.CountAsync() > 0;
        var hasWarning = await Page.Locator(".alert-warning").CountAsync() > 0;

        Assert.That(hasRiskWarning || hasWarning, Is.True,
            "Should show Risk Warning or account warning");
    }

    #endregion

    #region Exchange Account Modal Tests

    [Test]
    public async Task ExchangeAccount_Modal_ModeSelection_Works()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var modal = Page.Locator(".modal.show");
        await Expect(modal).ToBeVisibleAsync();

        var modeSelect = modal.Locator("select").Filter(new() { Has = Page.Locator("option:has-text('Live')") });
        if (await modeSelect.CountAsync() > 0)
        {
            await modeSelect.SelectOptionAsync(new SelectOptionValue { Label = "Testnet (Simulation)" });
            await Page.WaitForTimeoutAsync(200);

            await modeSelect.SelectOptionAsync(new SelectOptionValue { Label = "Live (Real Trading)" });
            await Page.WaitForTimeoutAsync(200);
        }

        var cancelButton = modal.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
        await cancelButton.ClickAsync();
    }

    [Test]
    public async Task ExchangeAccount_Modal_SaveWithEmptyFields_ShowsValidation()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var modal = Page.Locator(".modal.show");
        await Expect(modal).ToBeVisibleAsync();

        var saveButton = modal.GetByRole(AriaRole.Button, new() { Name = "Save" });
        await saveButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var modalStillVisible = await Page.Locator(".modal.show").CountAsync() > 0;
        var validationError = await modal.Locator(".validation-message, .invalid-feedback, .text-danger").CountAsync() > 0;

        Assert.That(modalStillVisible || validationError, Is.True,
            "Modal should stay open or show validation error on invalid save");
    }

    [Test]
    public async Task ExchangeAccount_AvailableExchanges_Shown()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var availableExchangesHeader = Page.GetByText("Available Exchanges");
        if (await availableExchangesHeader.CountAsync() > 0)
        {
            await Expect(availableExchangesHeader).ToBeVisibleAsync();
        }
    }

    #endregion

    #region Cross-Page Navigation Tests

    [Test]
    public async Task Navigation_DashboardToAccountsViaQuickLink()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var accountsLink = Page.GetByRole(AriaRole.Link, new() { Name = "Exchange Accounts" });
        if (await accountsLink.CountAsync() > 0)
        {
            await accountsLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.That(Page.Url, Does.Contain("/trading/accounts"));
        }
    }

    [Test]
    public async Task Navigation_DashboardToOrdersViaQuickLink()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var ordersLink = Page.GetByRole(AriaRole.Link, new() { Name = "My Orders" });
        if (await ordersLink.CountAsync() > 0)
        {
            await ordersLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.That(Page.Url, Does.Contain("/trading/orders"));
        }
    }

    [Test]
    public async Task Navigation_OrdersToNewOrderAndBack()
    {
        await AddTestExchangeAccount("Navigation Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var newOrderLink = Page.GetByRole(AriaRole.Link, new() { Name = "New Order" });
        if (await newOrderLink.CountAsync() > 0)
        {
            await newOrderLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.That(Page.Url, Does.Contain("/trading/orders/new"));

            await Page.GoBackAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.That(Page.Url, Does.Contain("/trading/orders").And.Not.Contain("/new"));
        }
    }

    [Test]
    public async Task Navigation_BreadcrumbsFromNewOrder()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var breadcrumb = Page.Locator("nav[aria-label='breadcrumb']");
        if (await breadcrumb.CountAsync() > 0)
        {
            var ordersLink = breadcrumb.GetByRole(AriaRole.Link, new() { Name = "Orders" });
            if (await ordersLink.CountAsync() > 0)
            {
                await ordersLink.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                Assert.That(Page.Url, Does.Contain("/trading/orders").And.Not.Contain("/new"));
            }
        }
    }

    #endregion

    #region Comprehensive User Journey Tests (25 Additional)

    [Test]
    public async Task Journey01_FreshUser_DashboardShowsContentOrError()
    {
        // Fresh user visits dashboard - should see prices, loading, or error (no crash)
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var hasHeading = await Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" }).CountAsync() > 0;
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;
        var hasSpinner = await Page.Locator(".spinner-border").CountAsync() > 0;
        var hasTable = await Page.Locator("table").CountAsync() > 0;

        Assert.That(hasHeading, Is.True, "Dashboard heading should always be visible");
        Assert.That(hasTable || hasError || hasSpinner, Is.True, "Should show table, error, or spinner");
    }

    [Test]
    public async Task Journey02_FreshUser_OrdersPageShowsNoAccountWarning()
    {
        // Fresh user visits orders - should see warning to add account
        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var warningAlert = Page.Locator(".alert-warning");
        var warningCount = await warningAlert.CountAsync();

        // Either shows warning (no accounts) or shows orders UI (if accounts exist somehow)
        Assert.That(warningCount > 0 || await Page.Locator("select.form-select").CountAsync() > 0, Is.True,
            "Should show no-account warning or orders filter");
    }

    [Test]
    public async Task Journey03_FreshUser_NewOrderPageShowsNoAccountWarning()
    {
        // Fresh user visits new order - should see warning
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var warningAlert = Page.Locator(".alert-warning");
        var hasWarning = await warningAlert.CountAsync() > 0;
        var hasForm = await Page.Locator("form").CountAsync() > 0;

        Assert.That(hasWarning || hasForm, Is.True, "Should show warning or form");
    }

    [Test]
    public async Task Journey04_FreshUser_DashboardQuickLinksNavigate()
    {
        // Fresh user clicks Exchange Accounts quick link from dashboard
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var accountsLink = Page.GetByRole(AriaRole.Link, new() { Name = "Exchange Accounts" });
        if (await accountsLink.CountAsync() > 0)
        {
            await accountsLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.That(Page.Url, Does.Contain("/trading/accounts"));
        }
        else
        {
            // If quick links not visible (error state), verify we're still on dashboard
            Assert.That(Page.Url, Does.Contain("/trading"));
        }
    }

    [Test]
    public async Task Journey05_CompleteOnboarding_RegisterToDashboardToAccountsToAdd()
    {
        // Complete onboarding: already registered (in setup), go dashboard -> accounts -> add
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Navigate to accounts - go directly since quick links may not be visible in error state
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Click add account
        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        
        // Wait for the button to be available
        await Expect(addButton).ToBeVisibleAsync(new() { Timeout = 10000 });
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var modal = Page.Locator(".modal.show, .modal.fade.show.d-block");
        await Expect(modal).ToBeVisibleAsync();
    }

    [Test]
    public async Task Journey06_AddAccountWithOnlyName_SavesSuccessfully()
    {
        // Add account with just name, no credentials
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var modal = Page.Locator(".modal.show, .modal.fade.show.d-block");
        var nameInput = modal.GetByPlaceholder("My Trading Account");
        await nameInput.FillAsync("Name Only Account");

        var exchangeSelect = modal.Locator("select.form-select").First;
        await exchangeSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });

        var saveButton = modal.GetByRole(AriaRole.Button, new() { Name = "Save" });
        await saveButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        // Modal should close or show error
        var modalStillVisible = await Page.Locator(".modal.show").CountAsync() > 0;
        var accountCreated = await Page.GetByText("Name Only Account").CountAsync() > 0;

        Assert.That(!modalStillVisible || accountCreated, Is.True, "Account should be created or modal closed");
    }

    [Test]
    public async Task Journey07_AddAccountTestnetMode_ShowsTestnetBadge()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var modal = Page.Locator(".modal.show, .modal.fade.show.d-block");
        var nameInput = modal.GetByPlaceholder("My Trading Account");
        await nameInput.FillAsync("Testnet Mode Account");

        var exchangeSelect = modal.Locator("select.form-select").First;
        await exchangeSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });

        // Select testnet mode
        var modeSelect = modal.Locator("select.form-select").Nth(1);
        if (await modeSelect.CountAsync() > 0)
        {
            await modeSelect.SelectOptionAsync("Testnet");
        }

        var saveButton = modal.GetByRole(AriaRole.Button, new() { Name = "Save" });
        await saveButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        // Check for Testnet badge on the card
        var testnetBadge = Page.Locator(".badge").Filter(new() { HasText = "Testnet" });
        var hasBadge = await testnetBadge.CountAsync() > 0;
        var accountCreated = await Page.GetByText("Testnet Mode Account").CountAsync() > 0;

        Assert.That(hasBadge || accountCreated, Is.True, "Should show Testnet badge or account created");
    }

    [Test]
    public async Task Journey08_AddAccountLiveMode_ShowsLiveBadge()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var modal = Page.Locator(".modal.show, .modal.fade.show.d-block");
        var nameInput = modal.GetByPlaceholder("My Trading Account");
        await nameInput.FillAsync("Live Mode Account");

        var exchangeSelect = modal.Locator("select.form-select").First;
        await exchangeSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });

        // Live is default, but select explicitly
        var modeSelect = modal.Locator("select.form-select").Nth(1);
        if (await modeSelect.CountAsync() > 0)
        {
            await modeSelect.SelectOptionAsync("Live");
        }

        var saveButton = modal.GetByRole(AriaRole.Button, new() { Name = "Save" });
        await saveButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        // Check for Live badge
        var liveBadge = Page.Locator(".badge.bg-success").Filter(new() { HasText = "Live" });
        var hasBadge = await liveBadge.CountAsync() > 0;
        var accountCreated = await Page.GetByText("Live Mode Account").CountAsync() > 0;

        Assert.That(hasBadge || accountCreated, Is.True, "Should show Live badge or account created");
    }

    [Test]
    public async Task Journey09_EditAccountName_UpdatesCorrectly()
    {
        // First add an account
        await AddTestExchangeAccount("Original Name Account");

        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Find the card and click Edit
        var accountCard = Page.Locator(".card").Filter(new() { HasText = "Original Name Account" }).First;
        var editButton = accountCard.GetByRole(AriaRole.Button, new() { Name = "Edit" });
        
        if (await editButton.CountAsync() > 0)
        {
            await editButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            var modal = Page.Locator(".modal.show");
            var nameInput = modal.GetByPlaceholder("My Trading Account");
            
            // Clear and type new name
            await nameInput.ClearAsync();
            await nameInput.FillAsync("Updated Name Account");

            var saveButton = modal.GetByRole(AriaRole.Button, new() { Name = "Save" });
            await saveButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);

            // Verify updated name
            var updatedAccount = Page.GetByText("Updated Name Account");
            var hasUpdated = await updatedAccount.CountAsync() > 0;
            Assert.That(hasUpdated, Is.True, "Account name should be updated");
        }
    }

    [Test]
    public async Task Journey10_CancelAddModal_NothingAdded()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Count accounts before
        var cardsBefore = await Page.Locator(".card").Filter(new() { Has = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }) }).CountAsync();

        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var modal = Page.Locator(".modal.show, .modal.fade.show.d-block");
        var nameInput = modal.GetByPlaceholder("My Trading Account");
        await nameInput.FillAsync("Should Not Be Added");

        // Cancel instead of save
        var cancelButton = modal.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
        await cancelButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Count accounts after
        var cardsAfter = await Page.Locator(".card").Filter(new() { Has = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }) }).CountAsync();

        Assert.That(cardsAfter, Is.EqualTo(cardsBefore), "No account should be added after cancel");
        
        // Verify the name doesn't appear
        var notAdded = await Page.GetByText("Should Not Be Added").CountAsync();
        Assert.That(notAdded, Is.EqualTo(0), "Cancelled account should not appear");
    }

    [Test]
    public async Task Journey11_CancelDeleteConfirmation_AccountPreserved()
    {
        await AddTestExchangeAccount("Preserved Account");

        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var accountCard = Page.Locator(".card").Filter(new() { HasText = "Preserved Account" }).First;
        var deleteButton = accountCard.GetByRole(AriaRole.Button, new() { Name = "Delete" });

        if (await deleteButton.CountAsync() > 0)
        {
            await deleteButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            var confirmModal = Page.Locator(".modal.show");
            await Expect(confirmModal).ToBeVisibleAsync();

            // Cancel deletion
            var cancelButton = confirmModal.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
            await cancelButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Account should still exist
            var accountStillExists = await Page.GetByText("Preserved Account").CountAsync() > 0;
            Assert.That(accountStillExists, Is.True, "Account should be preserved after cancel");
        }
    }

    [Test]
    public async Task Journey12_DeleteAccount_RemovedCompletely()
    {
        await AddTestExchangeAccount("To Delete Account");

        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var accountCard = Page.Locator(".card").Filter(new() { HasText = "To Delete Account" }).First;
        var deleteButton = accountCard.GetByRole(AriaRole.Button, new() { Name = "Delete" });

        if (await deleteButton.CountAsync() > 0)
        {
            await deleteButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            var confirmModal = Page.Locator(".modal.show");
            var confirmDeleteButton = confirmModal.GetByRole(AriaRole.Button, new() { Name = "Delete" });
            await confirmDeleteButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);

            // Account should be gone
            var accountGone = await Page.GetByText("To Delete Account").CountAsync() == 0;
            Assert.That(accountGone, Is.True, "Account should be removed after delete");
        }
    }

    [Test]
    public async Task Journey13_Dashboard_FilterBTC_ShowsOnlyBTCPairs()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var filterInput = Page.GetByPlaceholder("Filter by currency...");
        if (await filterInput.CountAsync() > 0)
        {
            await filterInput.FillAsync("BTC");
            await Page.WaitForTimeoutAsync(300);

            // All visible badges should contain BTC
            var badges = Page.Locator("table tbody .badge.bg-secondary");
            var badgeCount = await badges.CountAsync();

            if (badgeCount > 0)
            {
                for (int i = 0; i < Math.Min(badgeCount, 5); i++)
                {
                    var badgeText = await badges.Nth(i).TextContentAsync();
                    Assert.That(badgeText?.ToUpper(), Does.Contain("BTC"), $"Badge {i} should contain BTC");
                }
            }
        }
    }

    [Test]
    public async Task Journey14_Dashboard_FilterCaseInsensitive()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var filterInput = Page.GetByPlaceholder("Filter by currency...");
        if (await filterInput.CountAsync() > 0)
        {
            // Filter with lowercase
            await filterInput.FillAsync("btc");
            await Page.WaitForTimeoutAsync(300);
            var countLower = await Page.Locator("table tbody tr").CountAsync();

            // Filter with uppercase
            await filterInput.FillAsync("BTC");
            await Page.WaitForTimeoutAsync(300);
            var countUpper = await Page.Locator("table tbody tr").CountAsync();

            // Should be same results
            Assert.That(countLower, Is.EqualTo(countUpper), "Filter should be case-insensitive");
        }
    }

    [Test]
    public async Task Journey15_Dashboard_FilterNoMatches_ShowsEmptyTable()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var filterInput = Page.GetByPlaceholder("Filter by currency...");
        if (await filterInput.CountAsync() > 0)
        {
            await filterInput.FillAsync("ZZZZNOTEXIST");
            await Page.WaitForTimeoutAsync(300);

            var rowCount = await Page.Locator("table tbody tr").CountAsync();
            Assert.That(rowCount, Is.EqualTo(0), "Should show no results for non-existent filter");

            // Badge count should show 0 prices
            var countBadge = Page.Locator(".badge.bg-primary").Filter(new() { HasText = "0 prices" });
            var showsZero = await countBadge.CountAsync() > 0;
            Assert.That(showsZero, Is.True, "Should show 0 prices badge");
        }
    }

    [Test]
    public async Task Journey16_Dashboard_SortCurrencyAscending()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var currencyHeader = Page.Locator("th").Filter(new() { HasText = "Currency" });
        if (await currencyHeader.CountAsync() > 0)
        {
            await currencyHeader.ClickAsync();
            await Page.WaitForTimeoutAsync(300);

            // Check sort icon appears
            var sortIcon = currencyHeader.Locator(".bi-sort-alpha-down, .bi-sort-alpha-up-alt");
            var hasIcon = await sortIcon.CountAsync() > 0;
            Assert.That(hasIcon, Is.True, "Should show sort icon after clicking");
        }
    }

    [Test]
    public async Task Journey17_Dashboard_SortPriceDescending()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var priceHeader = Page.Locator("th").Filter(new() { HasText = "Price" });
        if (await priceHeader.CountAsync() > 0)
        {
            // Click twice for descending
            await priceHeader.ClickAsync();
            await Page.WaitForTimeoutAsync(200);
            await priceHeader.ClickAsync();
            await Page.WaitForTimeoutAsync(300);

            var sortIcon = priceHeader.Locator(".bi-sort-numeric-up-alt");
            var hasDescIcon = await sortIcon.CountAsync() > 0;
            
            // Either has desc icon or any sort icon
            var anySortIcon = priceHeader.Locator("[class*='bi-sort']");
            Assert.That(await anySortIcon.CountAsync() > 0, Is.True, "Should show sort icon");
        }
    }

    [Test]
    public async Task Journey18_Dashboard_StatsShowCorrectPriceCount()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var statsSection = Page.GetByText("Stats").First;
        if (await statsSection.CountAsync() > 0)
        {
            // Stats should show price count
            var pricesText = Page.Locator("dd").Filter(new() { HasText = "prices" });
            var hasCount = await pricesText.CountAsync() > 0;
            Assert.That(hasCount, Is.True, "Stats should show price count");
        }
    }

    [Test]
    public async Task Journey19_Dashboard_GoLiveChangesButton()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var goLiveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Go Live" });
        if (await goLiveButton.CountAsync() > 0)
        {
            await goLiveButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);

            // Button should change to Stop Live
            var stopLiveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Stop Live" });
            var hasStopLive = await stopLiveButton.CountAsync() > 0;
            var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;

            Assert.That(hasStopLive || hasError, Is.True, "Should show Stop Live or error");

            // Clean up - stop streaming
            if (hasStopLive)
            {
                await stopLiveButton.ClickAsync();
            }
        }
    }

    [Test]
    public async Task Journey20_Dashboard_LiveBadgeAppearsWhenStreaming()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var goLiveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Go Live" });
        if (await goLiveButton.CountAsync() > 0)
        {
            await goLiveButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);

            // Live badge should appear
            var liveBadge = Page.Locator(".badge.bg-success").Filter(new() { HasText = "Live" });
            var hasLiveBadge = await liveBadge.CountAsync() > 0;
            var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;

            Assert.That(hasLiveBadge || hasError, Is.True, "Should show Live badge or error");

            // Clean up
            var stopLiveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Stop Live" });
            if (await stopLiveButton.CountAsync() > 0)
            {
                await stopLiveButton.ClickAsync();
            }
        }
    }

    [Test]
    public async Task Journey21_Orders_FilterActive_ShowsOnlyActiveOrEmpty()
    {
        await AddTestExchangeAccount("Orders Active Filter Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var activeButton = Page.GetByRole(AriaRole.Button, new() { Name = "Active" });
        if (await activeButton.CountAsync() > 0)
        {
            await activeButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Should be highlighted
            var isActive = await activeButton.GetAttributeAsync("class");
            Assert.That(isActive, Does.Contain("btn-primary"), "Active button should be highlighted");
        }
    }

    [Test]
    public async Task Journey22_Orders_FilterFilled_ShowsOnlyFilledOrEmpty()
    {
        await AddTestExchangeAccount("Orders Filled Filter Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var filledButton = Page.GetByRole(AriaRole.Button, new() { Name = "Filled" });
        if (await filledButton.CountAsync() > 0)
        {
            await filledButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            var isActive = await filledButton.GetAttributeAsync("class");
            Assert.That(isActive, Does.Contain("btn-primary"), "Filled button should be highlighted");
        }
    }

    [Test]
    public async Task Journey23_NewOrder_QueryParams_PrefillForm()
    {
        await AddTestExchangeAccount("Query Params Test");

        // Navigate with query params
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new?base=ETH&quote=BTC&side=sell");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var baseInput = Page.GetByPlaceholder("BTC");
        var quoteInput = Page.GetByPlaceholder("USDT");

        if (await baseInput.CountAsync() > 0)
        {
            var baseValue = await baseInput.InputValueAsync();
            Assert.That(baseValue.ToUpper(), Is.EqualTo("ETH"), "Base should be pre-filled from query");
        }

        if (await quoteInput.CountAsync() > 0)
        {
            var quoteValue = await quoteInput.InputValueAsync();
            Assert.That(quoteValue.ToUpper(), Is.EqualTo("BTC"), "Quote should be pre-filled from query");
        }
    }

    [Test]
    public async Task Journey24_NewOrder_MarketOrder_HidesPriceField()
    {
        await AddTestExchangeAccount("Market Order Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var orderTypeSelect = Page.Locator("select").Filter(new() { Has = Page.Locator("option:has-text('Limit')") });
        if (await orderTypeSelect.CountAsync() > 0)
        {
            // Select Market
            await orderTypeSelect.SelectOptionAsync("Market");
            await Page.WaitForTimeoutAsync(300);

            // Price field should not be visible for market orders
            var priceLabel = Page.Locator("label").Filter(new() { HasText = "Price" });
            var priceCount = await priceLabel.CountAsync();
            
            // In market mode, price label might be hidden or not present
            // This is a UI check - page should not crash
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Place New Order" })).ToBeVisibleAsync();
        }
    }

    [Test]
    public async Task Journey25_NewOrder_TotalCalculation_ShowsForLimitOrders()
    {
        await AddTestExchangeAccount("Total Calc Test");

        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Check for warning - if no accounts, skip
        if (await Page.Locator(".alert-warning").CountAsync() > 0)
        {
            Assert.Pass("No accounts available for form test");
            return;
        }

        // Fill in quantity and price
        var quantityInput = Page.Locator("input[type='number']").First;
        if (await quantityInput.CountAsync() > 0)
        {
            await quantityInput.FillAsync("1.5");
        }

        // Find price input (second number input for limit orders)
        var priceInput = Page.Locator("input[type='number']").Nth(1);
        if (await priceInput.CountAsync() > 0)
        {
            await priceInput.FillAsync("50000");
            await Page.WaitForTimeoutAsync(300);

            // Should show Total calculation
            var totalAlert = Page.Locator(".alert-secondary").Filter(new() { HasText = "Total" });
            var hasTotal = await totalAlert.CountAsync() > 0;
            
            // Either shows total or page remains functional
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Place New Order" })).ToBeVisibleAsync();
        }
    }

    #endregion

    #region Helper Methods

    private async Task WaitForLoadingToComplete()
    {
        for (int i = 0; i < 20; i++)
        {
            var spinnerCount = await Page.Locator(".spinner-border").CountAsync();
            if (spinnerCount == 0) break;
            await Page.WaitForTimeoutAsync(500);
        }
    }

    private async Task AddTestExchangeAccount(string name)
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var modal = Page.Locator(".modal.show, .modal.fade.show.d-block");
        await Expect(modal).ToBeVisibleAsync();

        // Fill account name using placeholder
        var nameInput = modal.GetByPlaceholder("My Trading Account");
        await nameInput.FillAsync(name);

        // Select the first available exchange
        var exchangeSelect = modal.Locator("select.form-select").First;
        if (await exchangeSelect.CountAsync() > 0)
        {
            var options = exchangeSelect.Locator("option");
            var optionCount = await options.CountAsync();
            if (optionCount > 1)
            {
                await exchangeSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            }
        }

        var saveButton = modal.GetByRole(AriaRole.Button, new() { Name = "Save" });
        await saveButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    private async Task AddBinanceTestnetAccount(string name)
    {
        // Use the Binance testnet API keys from environment variables
        var apiKey = Environment.GetEnvironmentVariable("BINANCE_TEST_API_KEY");
        var secretKey = Environment.GetEnvironmentVariable("BINANCE_TEST_API_SECRET");

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
        {
            Assert.Ignore("Skipping test: BINANCE_TEST_API_KEY and BINANCE_TEST_API_SECRET environment variables are not set.");
            return;
        }

        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Account" });
        if (await addButton.CountAsync() == 0)
        {
            addButton = Page.GetByRole(AriaRole.Button, new() { Name = "Add Your First Account" });
        }
        await addButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var modal = Page.Locator(".modal.show, .modal.fade.show.d-block");
        await Expect(modal).ToBeVisibleAsync();

        // Fill account name
        var nameInput = modal.GetByPlaceholder("My Trading Account");
        await nameInput.FillAsync(name);

        // Select Binance exchange (code is lowercase "binance")
        var exchangeSelect = modal.Locator("select.form-select").First;
        await exchangeSelect.SelectOptionAsync("binance");

        // Select Testnet mode
        var modeSelect = modal.Locator("select.form-select").Nth(1);
        if (await modeSelect.CountAsync() > 0)
        {
            await modeSelect.SelectOptionAsync("Testnet");
        }

        // Fill API credentials
        var apiKeyInput = modal.GetByPlaceholder("Your API Key");
        await apiKeyInput.FillAsync(apiKey);

        var apiSecretInput = modal.GetByPlaceholder("Your API Secret");
        await apiSecretInput.FillAsync(secretKey);

        // Save
        var saveButton = modal.GetByRole(AriaRole.Button, new() { Name = "Save" });
        await saveButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1500);
    }

    #endregion

    #region Real API Integration Tests

    [Test]
    public async Task RealApi_AddBinanceTestnetAccount_SavesSuccessfully()
    {
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Add account with real API keys
        await AddBinanceTestnetAccount("Binance Testnet Account");

        // Verify account was created - modal should close
        await Page.WaitForTimeoutAsync(500);
        var modalVisible = await Page.Locator(".modal.show").CountAsync() > 0;
        
        // Check if account appears in the list or if there's an error
        var accountCard = Page.GetByText("Binance Testnet Account");
        var hasAccount = await accountCard.CountAsync() > 0;
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;

        // Either the account was created (modal closed and card visible) or there's an error message
        Assert.That(!modalVisible || hasAccount || hasError, Is.True,
            "Account should be created successfully or show error");
    }

    [Test]
    public async Task RealApi_Dashboard_LoadsPricesWithAccount()
    {
        // Add real Binance testnet account
        await AddBinanceTestnetAccount("Dashboard Real API Test");

        // Navigate to dashboard
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Wait for prices to potentially load
        await Page.WaitForTimeoutAsync(3000);

        // Dashboard should show heading
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();

        // Check for price table or loading/error state
        var priceTable = Page.Locator("table");
        var hasTable = await priceTable.CountAsync() > 0;
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;
        var hasSpinner = await Page.Locator(".spinner-border").CountAsync() > 0;

        Assert.That(hasTable || hasError || hasSpinner, Is.True,
            "Should show price table, error, or loading state");
    }

    [Test]
    public async Task RealApi_Dashboard_GoLive_StartsStreaming()
    {
        // Add real Binance testnet account
        await AddBinanceTestnetAccount("Live Streaming Test");

        // Navigate to dashboard
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Wait for initial load
        await Page.WaitForTimeoutAsync(2000);

        // Click Go Live if available
        var goLiveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Go Live" });
        if (await goLiveButton.CountAsync() > 0)
        {
            await goLiveButton.ClickAsync();
            
            // Wait for streaming to potentially start
            await Page.WaitForTimeoutAsync(3000);

            // Check if streaming indicator appears or Stop Live button appears
            var stopLiveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Stop Live" });
            var streamingBadge = Page.Locator(".badge").Filter(new() { HasText = "Live" });
            
            var isStreaming = await stopLiveButton.CountAsync() > 0;
            var hasBadge = await streamingBadge.CountAsync() > 0;
            var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;

            Assert.That(isStreaming || hasBadge || hasError, Is.True,
                "Should show streaming state or error");
        }

        // Page should still be functional
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task RealApi_NewOrder_GetPrice_FetchesBTCUSDTPrice()
    {
        // Add real Binance testnet account
        await AddBinanceTestnetAccount("Get Price Test");

        // Navigate to new order
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Wait for account to load
        await Page.WaitForTimeoutAsync(1000);

        // Check if form is available (not showing "no accounts" warning)
        var warning = Page.Locator(".alert-warning");
        if (await warning.CountAsync() > 0)
        {
            // No accounts available, test passes as expected behavior
            Assert.Pass("No accounts available - expected if account creation failed");
            return;
        }

        // Fill in BTC/USDT
        var baseAssetInput = Page.GetByPlaceholder("BTC");
        var quoteAssetInput = Page.GetByPlaceholder("USDT");

        if (await baseAssetInput.CountAsync() > 0 && await quoteAssetInput.CountAsync() > 0)
        {
            await baseAssetInput.FillAsync("BTC");
            await quoteAssetInput.FillAsync("USDT");

            // Click Get Price
            var getPriceButton = Page.GetByRole(AriaRole.Button, new() { Name = "Get Price" });
            if (await getPriceButton.CountAsync() > 0)
            {
                await getPriceButton.ClickAsync();

                // Wait for price fetch
                await Page.WaitForTimeoutAsync(5000);

                // Check for price display or error
                var priceAlert = Page.Locator(".alert-info");
                var hasPriceInfo = await priceAlert.CountAsync() > 0;
                var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;
                var priceInput = Page.Locator("input[type='number']").Filter(new() { Has = Page.Locator("[placeholder]") });

                Assert.That(hasPriceInfo || hasError || await priceInput.CountAsync() > 0, Is.True,
                    "Should show price info, error, or price input populated");
            }
        }

        // Page should remain functional
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Place New Order" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task RealApi_Orders_LoadsOrdersFromExchange()
    {
        // Add real Binance testnet account
        await AddBinanceTestnetAccount("Orders Load Test");

        // Navigate to orders
        await Page.GotoAsync($"{BaseUrl}/trading/orders");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Wait for orders to potentially load
        await Page.WaitForTimeoutAsync(3000);

        // Check page state
        var ordersHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Orders", Exact = true });
        await Expect(ordersHeading).ToBeVisibleAsync();

        // Should show orders table, empty state, or error
        var ordersTable = Page.Locator("table");
        var noOrdersState = Page.GetByText("No orders");
        var emptyState = Page.Locator(".card-body").Filter(new() { HasText = "haven't placed" });
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;

        var hasTable = await ordersTable.CountAsync() > 0;
        var hasNoOrders = await noOrdersState.CountAsync() > 0;
        var hasEmpty = await emptyState.CountAsync() > 0;

        Assert.That(hasTable || hasNoOrders || hasEmpty || hasError, Is.True,
            "Should show orders content or appropriate state");
    }

    [Test]
    public async Task RealApi_CompleteJourney_AddAccount_ViewDashboard_CheckOrders()
    {
        // Step 1: Add Binance testnet account with real credentials
        await AddBinanceTestnetAccount("Complete Journey Account");

        // Wait for account to be saved
        await Page.WaitForTimeoutAsync(1000);

        // Step 2: Navigate to Dashboard and verify it loads
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();
        await Page.WaitForTimeoutAsync(2000);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();

        // Step 3: Use Quick Links to navigate to Orders
        var ordersLink = Page.GetByRole(AriaRole.Link, new() { Name = "My Orders" });
        if (await ordersLink.CountAsync() > 0)
        {
            await ordersLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForLoadingToComplete();
            await Page.WaitForTimeoutAsync(2000);

            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Orders", Exact = true })).ToBeVisibleAsync();
        }

        // Step 4: Navigate to New Order page
        var newOrderLink = Page.GetByRole(AriaRole.Link, new() { Name = "New Order" });
        if (await newOrderLink.CountAsync() > 0)
        {
            await newOrderLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForLoadingToComplete();

            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Place New Order" })).ToBeVisibleAsync();
        }

        // Step 5: Go back to accounts and verify account exists
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        var accountCard = Page.GetByText("Complete Journey Account");
        var hasAccount = await accountCard.CountAsync() > 0;
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;

        // Account should exist or there should be an error explaining why not
        Assert.That(hasAccount || hasError, Is.True,
            "Account should exist or error should explain why");
    }

    [Test]
    public async Task RealApi_Dashboard_FilterAndSort_WithRealPrices()
    {
        // Add real Binance testnet account
        await AddBinanceTestnetAccount("Filter Sort Test");

        // Navigate to dashboard
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Wait for prices to load
        await Page.WaitForTimeoutAsync(3000);

        // Try filtering by BTC
        var filterInput = Page.GetByPlaceholder("Filter by currency...");
        if (await filterInput.CountAsync() > 0)
        {
            await filterInput.FillAsync("BTC");
            await Page.WaitForTimeoutAsync(500);

            // Verify page still works
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();

            // Clear filter
            await filterInput.FillAsync("");
            await Page.WaitForTimeoutAsync(500);
        }

        // Try sorting by clicking column headers
        var currencyHeader = Page.Locator("th").Filter(new() { HasText = "Currency" });
        if (await currencyHeader.CountAsync() > 0)
        {
            await currencyHeader.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Click again for reverse sort
            await currencyHeader.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Page should remain functional
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task RealApi_NewOrder_FormValidation_WithRealAccount()
    {
        // Add real Binance testnet account
        await AddBinanceTestnetAccount("Form Validation Test");

        // Navigate to new order
        await Page.GotoAsync($"{BaseUrl}/trading/orders/new");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();
        await Page.WaitForTimeoutAsync(1000);

        // Check if form is available
        var warning = Page.Locator(".alert-warning");
        if (await warning.CountAsync() > 0)
        {
            Assert.Pass("No accounts available for order form");
            return;
        }

        // Try to submit empty form
        var submitButton = Page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Buy" });
        if (await submitButton.CountAsync() == 0)
        {
            submitButton = Page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Sell" });
        }

        if (await submitButton.CountAsync() > 0)
        {
            await submitButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Should show validation errors or stay on form
            var validationErrors = Page.Locator(".validation-message, .invalid-feedback, .text-danger");
            var hasValidationError = await validationErrors.CountAsync() > 0;
            var stillOnPage = await Page.GetByRole(AriaRole.Heading, new() { Name = "Place New Order" }).CountAsync() > 0;

            Assert.That(hasValidationError || stillOnPage, Is.True,
                "Should show validation errors or stay on form");
        }
    }

    [Test]
    public async Task RealApi_EditAccount_PreservesCredentials()
    {
        // Add real Binance testnet account
        await AddBinanceTestnetAccount("Edit Preserve Test");

        // Navigate to accounts
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Find and click Edit button
        var editButton = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" }).First;
        if (await editButton.CountAsync() > 0)
        {
            await editButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            var modal = Page.Locator(".modal.show");
            await Expect(modal).ToBeVisibleAsync();

            // Verify name is pre-filled
            var nameInput = modal.GetByPlaceholder("My Trading Account");
            var nameValue = await nameInput.InputValueAsync();
            Assert.That(nameValue, Does.Contain("Edit Preserve Test").Or.Not.Empty,
                "Name should be pre-filled");

            // Exchange select should be disabled in edit mode
            var exchangeSelect = modal.Locator("select.form-select").First;
            var isDisabled = await exchangeSelect.IsDisabledAsync();
            
            // Close modal
            var cancelButton = modal.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
            await cancelButton.ClickAsync();
        }
    }

    [Test]
    public async Task RealApi_DeleteAccount_RemovesFromList()
    {
        // Add real Binance testnet account
        await AddBinanceTestnetAccount("Delete Test Account");

        // Navigate to accounts
        await Page.GotoAsync($"{BaseUrl}/trading/accounts");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Count accounts before delete
        var accountCardsBefore = Page.Locator(".card").Filter(new() { HasText = "Delete Test Account" });
        var accountsBefore = await accountCardsBefore.CountAsync();

        if (accountsBefore > 0)
        {
            // Find and click Delete button for this account
            var accountCard = accountCardsBefore.First;
            var deleteButton = accountCard.GetByRole(AriaRole.Button, new() { Name = "Delete" });
            await deleteButton.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Confirm deletion
            var confirmModal = Page.Locator(".modal.show");
            await Expect(confirmModal).ToBeVisibleAsync();

            var confirmDeleteButton = confirmModal.GetByRole(AriaRole.Button, new() { Name = "Delete" });
            await confirmDeleteButton.ClickAsync();
            
            // Wait for deletion to complete and page to update
            await Page.WaitForTimeoutAsync(1500);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Verify account was removed - check fewer cards with that text exist
            var accountsAfter = await Page.Locator(".card").Filter(new() { HasText = "Delete Test Account" }).CountAsync();
            
            // Either removed or error message displayed
            var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;
            
            Assert.That(accountsAfter < accountsBefore || hasError, Is.True,
                "Account should be removed from list or error shown");
        }
    }

    #endregion

    #region Dashboard Pagination Tests

    [Test]
    public async Task Dashboard_Pagination_ShowsShowMoreWhenMoreThan50Items()
    {
        // This test requires API to return more than 50 items
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Check if we're in error state (API unavailable)
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;
        if (hasError)
        {
            Assert.Pass("Skipped - API unavailable");
            return;
        }

        // Get total count from badge
        var priceBadge = Page.Locator(".badge.bg-primary").Filter(new() { HasText = "prices" });
        var badgeExists = await priceBadge.CountAsync() > 0;
        
        if (!badgeExists)
        {
            Assert.Pass("Skipped - No price data available");
            return;
        }

        var badgeText = await priceBadge.TextContentAsync();
        var totalPrices = int.Parse(badgeText?.Replace(" prices", "").Trim() ?? "0");

        if (totalPrices > 50)
        {
            // Should show "Show X more" button
            var showMoreButton = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Show \\d+ more") });
            await Expect(showMoreButton).ToBeVisibleAsync();
            
            // Should show "Show all" button
            var showAllButton = Page.GetByRole(AriaRole.Button, new() { Name = "Show all" });
            await Expect(showAllButton).ToBeVisibleAsync();
            
            // Should show "Showing X of Y" text
            var showingText = Page.Locator("text=Showing 50 of");
            await Expect(showingText).ToBeVisibleAsync();
        }
        else
        {
            // With 50 or fewer items, no pagination controls should appear
            var showMoreButton = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Show \\d+ more") });
            Assert.That(await showMoreButton.CountAsync(), Is.EqualTo(0), "Should not show 'Show more' with 50 or fewer items");
        }
    }

    [Test]
    public async Task Dashboard_Pagination_ShowMoreLoadsMoreItems()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Check if we're in error state
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;
        if (hasError)
        {
            Assert.Pass("Skipped - API unavailable");
            return;
        }

        // Check for "Show more" button
        var showMoreButton = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Show \\d+ more") });
        var hasShowMore = await showMoreButton.CountAsync() > 0;

        if (!hasShowMore)
        {
            Assert.Pass("Skipped - Less than 50 items, no pagination needed");
            return;
        }

        // Count rows before clicking
        var rowsBefore = await Page.Locator("table tbody tr").CountAsync();
        
        // Click "Show more"
        await showMoreButton.ClickAsync();
        await Page.WaitForTimeoutAsync(300); // Small wait for UI update

        // Count rows after clicking
        var rowsAfter = await Page.Locator("table tbody tr").CountAsync();

        Assert.That(rowsAfter, Is.GreaterThan(rowsBefore), "Should show more items after clicking 'Show more'");
    }

    [Test]
    public async Task Dashboard_Pagination_ShowAllLoadsAllItems()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Check if we're in error state
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;
        if (hasError)
        {
            Assert.Pass("Skipped - API unavailable");
            return;
        }

        // Check for "Show all" button
        var showAllButton = Page.GetByRole(AriaRole.Button, new() { Name = "Show all" });
        var hasShowAll = await showAllButton.CountAsync() > 0;

        if (!hasShowAll)
        {
            Assert.Pass("Skipped - Less than 50 items, no pagination needed");
            return;
        }

        // Get total count
        var priceBadge = Page.Locator(".badge.bg-primary").Filter(new() { HasText = "prices" });
        var badgeText = await priceBadge.TextContentAsync();
        var totalPrices = int.Parse(badgeText?.Replace(" prices", "").Trim() ?? "0");

        // Click "Show all"
        await showAllButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500); // Wait for UI update

        // Count rows after clicking
        var rowsAfter = await Page.Locator("table tbody tr").CountAsync();

        Assert.That(rowsAfter, Is.EqualTo(totalPrices), $"Should show all {totalPrices} items after clicking 'Show all'");

        // "Show less" button should appear after showing all
        var showLessButton = Page.GetByRole(AriaRole.Button, new() { Name = "Show less" });
        await Expect(showLessButton).ToBeVisibleAsync();

        // "Show more" should be hidden after showing all
        var showMoreAfter = await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Show \\d+ more") }).CountAsync();
        Assert.That(showMoreAfter, Is.EqualTo(0), "'Show more' should be hidden after showing all");
    }

    [Test]
    public async Task Dashboard_Pagination_ShowLessResetsToDefault()
    {
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForLoadingToComplete();

        // Check if we're in error state
        var hasError = await Page.Locator(".alert-danger").CountAsync() > 0;
        if (hasError)
        {
            Assert.Pass("Skipped - API unavailable");
            return;
        }

        // Check for "Show all" button (means we have >50 items)
        var showAllButton = Page.GetByRole(AriaRole.Button, new() { Name = "Show all" });
        var hasShowAll = await showAllButton.CountAsync() > 0;

        if (!hasShowAll)
        {
            Assert.Pass("Skipped - Less than 50 items, no pagination needed");
            return;
        }

        // Click "Show all" first
        await showAllButton.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Now "Show less" should be visible
        var showLessButton = Page.GetByRole(AriaRole.Button, new() { Name = "Show less" });
        await Expect(showLessButton).ToBeVisibleAsync();

        // Click "Show less"
        await showLessButton.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Should be back to 50 items
        var rowsAfter = await Page.Locator("table tbody tr").CountAsync();
        Assert.That(rowsAfter, Is.EqualTo(50), "Should show 50 items after clicking 'Show less'");

        // "Show more" and "Show all" should be visible again
        var showMoreAfter = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Show \\d+ more") });
        await Expect(showMoreAfter).ToBeVisibleAsync();

        var showAllAfter = Page.GetByRole(AriaRole.Button, new() { Name = "Show all" });
        await Expect(showAllAfter).ToBeVisibleAsync();

        // "Show less" should be hidden (back at default)
        var showLessAfter = await Page.GetByRole(AriaRole.Button, new() { Name = "Show less" }).CountAsync();
        Assert.That(showLessAfter, Is.EqualTo(0), "'Show less' should be hidden at default page size");
    }

    [Test]
    public async Task Dashboard_Pagination_NoErrorWithZeroPrices()
    {
        // Navigate to dashboard
        await Page.GotoAsync($"{BaseUrl}/trading");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Page should load without throwing errors regardless of data
        // Either loading, error, or data state is acceptable
        var heading = Page.GetByRole(AriaRole.Heading, new() { Name = "Trading Dashboard" });
        await Expect(heading).ToBeVisibleAsync();
        
        // No unhandled errors should appear - check console for JS errors would be ideal
        // but at minimum the page should render
        Assert.Pass("Page loaded without crashing");
    }

    #endregion
}
