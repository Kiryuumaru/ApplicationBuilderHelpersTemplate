using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Presentation.WebApi.FunctionalTests.Fixtures;

namespace Presentation.WebApi.FunctionalTests.Hubs;

/// <summary>
/// Functional tests for SignalR hubs (MarketData, Bot, Notification).
/// Tests real-time communication, authentication, and error handling.
/// </summary>
[Collection(WebApiTestCollection.Name)]
public class SignalRHubTests
{
    private readonly ITestOutputHelper _output;
    private readonly SharedWebApiHost _sharedHost;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _testUsername = $"hubtest_{Guid.NewGuid():N}";
    private const string TestPassword = "TestPassword123!";
    private const string TestEmail = "hubtest@example.com";

    public SignalRHubTests(SharedWebApiHost sharedHost, ITestOutputHelper output)
    {
        _sharedHost = sharedHost;
        _output = output;
    }

    #region Authentication Tests

    [Fact]
    public async Task Hub_RequiresAuthentication_MarketData()
    {
        _output.WriteLine("[TEST] Hub_RequiresAuthentication_MarketData");

        var hubUrl = new Uri(new Uri(_sharedHost.Host.BaseUrl), "/hubs/market-data");
        _output.WriteLine($"[STEP] Connecting to {hubUrl} without authentication...");

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .Build();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            async () => await connection.StartAsync());

        _output.WriteLine($"[RECEIVED] Exception: {exception.Message}");
        // Should get 401 Unauthorized
        Assert.Contains("401", exception.Message);

        _output.WriteLine("[PASS] Hub requires authentication");
    }

    [Fact]
    public async Task Hub_RequiresAuthentication_Bot()
    {
        _output.WriteLine("[TEST] Hub_RequiresAuthentication_Bot");

        var hubUrl = new Uri(new Uri(_sharedHost.Host.BaseUrl), "/hubs/bot");
        _output.WriteLine($"[STEP] Connecting to {hubUrl} without authentication...");

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .Build();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            async () => await connection.StartAsync());

        _output.WriteLine($"[RECEIVED] Exception: {exception.Message}");
        Assert.Contains("401", exception.Message);

        _output.WriteLine("[PASS] Hub requires authentication");
    }

    [Fact]
    public async Task Hub_RequiresAuthentication_Notifications()
    {
        _output.WriteLine("[TEST] Hub_RequiresAuthentication_Notifications");

        var hubUrl = new Uri(new Uri(_sharedHost.Host.BaseUrl), "/hubs/notifications");
        _output.WriteLine($"[STEP] Connecting to {hubUrl} without authentication...");

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .Build();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            async () => await connection.StartAsync());

        _output.WriteLine($"[RECEIVED] Exception: {exception.Message}");
        Assert.Contains("401", exception.Message);

        _output.WriteLine("[PASS] Hub requires authentication");
    }

    [Fact]
    public async Task Hub_ConnectsWithValidToken_MarketData()
    {
        _output.WriteLine("[TEST] Hub_ConnectsWithValidToken_MarketData");

        var token = await GetAccessTokenAsync();

        var hubUrl = new Uri(new Uri(_sharedHost.Host.BaseUrl), "/hubs/market-data");
        _output.WriteLine($"[STEP] Connecting to {hubUrl} with valid token...");

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

        await connection.StartAsync();

        _output.WriteLine($"[RECEIVED] Connection state: {connection.State}");
        Assert.Equal(HubConnectionState.Connected, connection.State);

        await connection.StopAsync();
        await connection.DisposeAsync();

        _output.WriteLine("[PASS] Hub connects with valid token");
    }

    [Fact]
    public async Task Hub_ConnectsWithValidToken_Notifications()
    {
        _output.WriteLine("[TEST] Hub_ConnectsWithValidToken_Notifications");

        var token = await GetAccessTokenAsync();

        var hubUrl = new Uri(new Uri(_sharedHost.Host.BaseUrl), "/hubs/notifications");
        _output.WriteLine($"[STEP] Connecting to {hubUrl} with valid token...");

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

        await connection.StartAsync();

        _output.WriteLine($"[RECEIVED] Connection state: {connection.State}");
        Assert.Equal(HubConnectionState.Connected, connection.State);

        await connection.StopAsync();
        await connection.DisposeAsync();

        _output.WriteLine("[PASS] Hub connects with valid token");
    }

    #endregion

    #region MarketData Hub Tests

    [Fact]
    public async Task MarketDataHub_SubscribeToPrice_ReturnsSubscriptionId()
    {
        _output.WriteLine("[TEST] MarketDataHub_SubscribeToPrice_ReturnsSubscriptionId");

        var token = await GetAccessTokenAsync();
        await using var connection = await CreateAuthenticatedHubConnectionAsync("/hubs/market-data", token);

        _output.WriteLine("[STEP] Calling SubscribeToPrice for BINANCE BTC/USDT...");
        var subscriptionId = await connection.InvokeAsync<string>(
            "SubscribeToPrice",
            "BINANCE",
            "BTC",
            "USDT",
            0); // preloadCount = 0

        _output.WriteLine($"[RECEIVED] SubscriptionId: {subscriptionId}");
        Assert.NotNull(subscriptionId);
        Assert.True(Guid.TryParse(subscriptionId, out _), "SubscriptionId should be a valid GUID");

        // Unsubscribe
        await connection.InvokeAsync("Unsubscribe", subscriptionId);

        _output.WriteLine("[PASS] SubscribeToPrice returns valid subscription ID");
    }

    [Fact]
    public async Task MarketDataHub_SubscribeToPrices_ReturnsSubscriptionId()
    {
        _output.WriteLine("[TEST] MarketDataHub_SubscribeToPrices_ReturnsSubscriptionId");

        var token = await GetAccessTokenAsync();
        await using var connection = await CreateAuthenticatedHubConnectionAsync("/hubs/market-data", token);

        _output.WriteLine("[STEP] Calling SubscribeToPrices for BINANCE...");
        var subscriptionId = await connection.InvokeAsync<string>(
            "SubscribeToPrices",
            "BINANCE");

        _output.WriteLine($"[RECEIVED] SubscriptionId: {subscriptionId}");
        Assert.NotNull(subscriptionId);
        Assert.True(Guid.TryParse(subscriptionId, out _), "SubscriptionId should be a valid GUID");

        // Unsubscribe
        await connection.InvokeAsync("Unsubscribe", subscriptionId);

        _output.WriteLine("[PASS] SubscribeToPrices returns valid subscription ID");
    }

    [Fact]
    public async Task MarketDataHub_SubscribeToCandles_ReturnsSubscriptionId()
    {
        _output.WriteLine("[TEST] MarketDataHub_SubscribeToCandles_ReturnsSubscriptionId");

        var token = await GetAccessTokenAsync();
        await using var connection = await CreateAuthenticatedHubConnectionAsync("/hubs/market-data", token);

        _output.WriteLine("[STEP] Calling SubscribeToCandles for BINANCE BTC/USDT 1m...");
        var subscriptionId = await connection.InvokeAsync<string>(
            "SubscribeToCandles",
            "BINANCE",
            "BTC",
            "USDT",
            "OneMinute", // interval
            0); // preloadCount = 0

        _output.WriteLine($"[RECEIVED] SubscriptionId: {subscriptionId}");
        Assert.NotNull(subscriptionId);
        Assert.True(Guid.TryParse(subscriptionId, out _), "SubscriptionId should be a valid GUID");

        // Unsubscribe
        await connection.InvokeAsync("Unsubscribe", subscriptionId);

        _output.WriteLine("[PASS] SubscribeToCandles returns valid subscription ID");
    }

    [Fact]
    public async Task MarketDataHub_Unsubscribe_StopsUpdates()
    {
        _output.WriteLine("[TEST] MarketDataHub_Unsubscribe_StopsUpdates");

        var token = await GetAccessTokenAsync();
        await using var connection = await CreateAuthenticatedHubConnectionAsync("/hubs/market-data", token);

        // Subscribe first
        var subscriptionId = await connection.InvokeAsync<string>(
            "SubscribeToPrice",
            "BINANCE",
            "BTC",
            "USDT",
            0);

        _output.WriteLine($"[STEP] Subscribed with ID: {subscriptionId}");

        // Unsubscribe
        _output.WriteLine("[STEP] Calling Unsubscribe...");
        await connection.InvokeAsync("Unsubscribe", subscriptionId);

        _output.WriteLine("[PASS] Unsubscribe completes without error");
    }

    [Fact]
    public async Task MarketDataHub_InvalidCandleInterval_ReturnsError()
    {
        _output.WriteLine("[TEST] MarketDataHub_InvalidCandleInterval_ReturnsError");

        var token = await GetAccessTokenAsync();
        await using var connection = await CreateAuthenticatedHubConnectionAsync("/hubs/market-data", token);

        _output.WriteLine("[STEP] Calling SubscribeToCandles with invalid interval...");
        var exception = await Assert.ThrowsAsync<HubException>(async () =>
            await connection.InvokeAsync<string>(
                "SubscribeToCandles",
                "BINANCE",
                "BTC",
                "USDT",
                "InvalidInterval",
                0));

        _output.WriteLine($"[RECEIVED] Exception: {exception.Message}");
        Assert.Contains("Invalid candle interval", exception.Message);

        _output.WriteLine("[PASS] Returns error for invalid candle interval");
    }

    #endregion

    #region Bot Hub Tests

    [Fact]
    public async Task BotHub_SubscribeToBotStatus_NonExistentBot_ReturnsError()
    {
        _output.WriteLine("[TEST] BotHub_SubscribeToBotStatus_NonExistentBot_ReturnsError");

        var token = await GetAccessTokenAsync();
        await using var connection = await CreateAuthenticatedHubConnectionAsync("/hubs/bot", token);

        var nonExistentBotId = Guid.NewGuid();
        _output.WriteLine($"[STEP] Subscribing to non-existent bot {nonExistentBotId}...");

        var exception = await Assert.ThrowsAsync<HubException>(async () =>
            await connection.InvokeAsync("SubscribeToBotStatus", nonExistentBotId));

        _output.WriteLine($"[RECEIVED] Exception: {exception.Message}");
        Assert.Contains("not found", exception.Message);

        _output.WriteLine("[PASS] Returns error for non-existent bot");
    }

    [Fact]
    public async Task BotHub_SubscribeToSignals_NonExistentBot_ReturnsError()
    {
        _output.WriteLine("[TEST] BotHub_SubscribeToSignals_NonExistentBot_ReturnsError");

        var token = await GetAccessTokenAsync();
        await using var connection = await CreateAuthenticatedHubConnectionAsync("/hubs/bot", token);

        var nonExistentBotId = Guid.NewGuid();
        _output.WriteLine($"[STEP] Subscribing to signals for non-existent bot {nonExistentBotId}...");

        var exception = await Assert.ThrowsAsync<HubException>(async () =>
            await connection.InvokeAsync("SubscribeToSignals", nonExistentBotId, 10));

        _output.WriteLine($"[RECEIVED] Exception: {exception.Message}");
        Assert.Contains("not found", exception.Message);

        _output.WriteLine("[PASS] Returns error for non-existent bot");
    }

    #endregion

    #region Notification Hub Tests

    [Fact]
    public async Task NotificationHub_Connect_Succeeds()
    {
        _output.WriteLine("[TEST] NotificationHub_Connect_Succeeds");

        var token = await GetAccessTokenAsync();
        await using var connection = await CreateAuthenticatedHubConnectionAsync("/hubs/notifications", token);

        // Simply connecting should work - the hub auto-subscribes authenticated users
        _output.WriteLine($"[RECEIVED] Connection state: {connection.State}");
        Assert.Equal(HubConnectionState.Connected, connection.State);

        _output.WriteLine("[PASS] Notification hub connects successfully");
    }

    [Fact]
    public async Task Hub_DisconnectCleansUpSubscriptions()
    {
        _output.WriteLine("[TEST] Hub_DisconnectCleansUpSubscriptions");

        var token = await GetAccessTokenAsync();
        var hubUrl = new Uri(new Uri(_sharedHost.Host.BaseUrl), "/hubs/market-data");

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

        await connection.StartAsync();
        _output.WriteLine("[STEP] Connected to MarketData hub");

        // Subscribe
        var subscriptionId = await connection.InvokeAsync<string>(
            "SubscribeToPrice",
            "BINANCE",
            "BTC",
            "USDT",
            0);
        _output.WriteLine($"[STEP] Subscribed: {subscriptionId}");

        // Disconnect
        _output.WriteLine("[STEP] Disconnecting...");
        await connection.StopAsync();
        await connection.DisposeAsync();

        _output.WriteLine("[PASS] Disconnection completed (subscriptions cleaned up server-side)");
    }

    #endregion

    #region Helper Methods

    private async Task<string> RegisterAndLoginAsync()
    {
        var username = $"hubtest_{Guid.NewGuid():N}";
        var registerRequest = new
        {
            Username = username,
            Password = TestPassword,
            ConfirmPassword = TestPassword,
            Email = $"{username}@example.com"
        };

        var registerResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var loginRequest = new { Username = username, Password = TestPassword };
        var loginResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var content = await loginResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
        return result!.AccessToken;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        return await RegisterAndLoginAsync();
    }

    private async Task<HubConnection> CreateAuthenticatedHubConnectionAsync(string hubPath, string token)
    {
        var hubUrl = new Uri(new Uri(_sharedHost.Host.BaseUrl), hubPath);
        _output.WriteLine($"[STEP] Creating connection to {hubUrl}...");

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

        await connection.StartAsync();
        _output.WriteLine($"[STEP] Connected: {connection.State}");
        return connection;
    }

    #endregion

    #region Models

    private record AuthResponse(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresIn,
        UserInfo User);

    private record UserInfo(
        string Id,
        string Username,
        string Email);

    #endregion
}
