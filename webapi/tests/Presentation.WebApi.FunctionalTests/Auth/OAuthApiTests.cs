using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Presentation.WebApi.FunctionalTests.Auth;

/// <summary>
/// Functional tests for OAuth/External Login API endpoints.
/// Tests external login providers, linking, and unlinking.
/// </summary>
[Collection(WebApiTestCollection.Name)]
public class OAuthApiTests
{
    private readonly ITestOutputHelper _output;
    private readonly SharedWebApiHost _sharedHost;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string TestPassword = "TestPassword123!";

    public OAuthApiTests(SharedWebApiHost sharedHost, ITestOutputHelper output)
    {
        _sharedHost = sharedHost;
        _output = output;
    }

    #region Get Providers Tests

    [Fact]
    public async Task GetProviders_ReturnsAvailableProviders()
    {
        _output.WriteLine("[TEST] GetProviders_ReturnsAvailableProviders");

        _output.WriteLine("[STEP] GET /api/v1/auth/external/providers...");
        var response = await _sharedHost.Host.HttpClient.GetAsync("/api/v1/auth/external/providers");

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {content}");

        var result = JsonSerializer.Deserialize<OAuthProvidersResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Providers);

        // Mock provider should be enabled by default
        var mockProvider = result.Providers.FirstOrDefault(p => p.Provider.Equals("mock", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(mockProvider);
        Assert.True(mockProvider!.IsEnabled, "Mock provider should be enabled by default");

        _output.WriteLine("[PASS] Get providers returned available providers with Mock enabled");
    }

    [Fact]
    public async Task GetProviders_NoAuthRequired()
    {
        _output.WriteLine("[TEST] GetProviders_NoAuthRequired");

        // This endpoint should be accessible without authentication
        _output.WriteLine("[STEP] GET /api/v1/auth/external/providers without token...");
        var response = await _sharedHost.Host.HttpClient.GetAsync("/api/v1/auth/external/providers");

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _output.WriteLine("[PASS] Get providers accessible without authentication");
    }

    #endregion

    #region Initiate OAuth Tests

    [Fact]
    public async Task InitiateOAuth_WithMockProvider_ReturnsAuthorizationUrl()
    {
        _output.WriteLine("[TEST] InitiateOAuth_WithMockProvider_ReturnsAuthorizationUrl");

        var request = new OAuthLoginRequest("mock", "https://localhost/callback");

        _output.WriteLine("[STEP] POST /api/v1/auth/external/mock...");
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/external/mock", request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {content}");

        var result = JsonSerializer.Deserialize<OAuthAuthorizationResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.NotEmpty(result!.AuthorizationUrl);
        Assert.NotEmpty(result.State);

        _output.WriteLine("[PASS] Initiate OAuth returned authorization URL with state");
    }

    [Fact]
    public async Task InitiateOAuth_WithInvalidProvider_Returns400()
    {
        _output.WriteLine("[TEST] InitiateOAuth_WithInvalidProvider_Returns400");

        var request = new OAuthLoginRequest("invalidprovider", "https://localhost/callback");

        _output.WriteLine("[STEP] POST /api/v1/auth/external/invalidprovider...");
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/external/invalidprovider", request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _output.WriteLine("[PASS] Returns 400 for invalid provider");
    }

    [Fact]
    public async Task InitiateOAuth_WithDisabledProvider_Returns400()
    {
        _output.WriteLine("[TEST] InitiateOAuth_WithDisabledProvider_Returns400");

        var request = new OAuthLoginRequest("google", "https://localhost/callback");

        // Google is not configured, so it should be disabled
        _output.WriteLine("[STEP] POST /api/v1/auth/external/google...");
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/external/google", request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _output.WriteLine("[PASS] Returns 400 for disabled provider");
    }

    #endregion

    #region OAuth Callback Tests

    [Fact]
    public async Task OAuthCallback_WithMockProvider_CreatesNewUserAndSession()
    {
        _output.WriteLine("[TEST] OAuthCallback_WithMockProvider_CreatesNewUserAndSession");

        // First, initiate OAuth to get a state
        var initiateRequest = new OAuthLoginRequest("mock", "https://localhost/callback");
        var initiateResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/external/mock", initiateRequest);
        Assert.Equal(HttpStatusCode.OK, initiateResponse.StatusCode);
        var initiateContent = await initiateResponse.Content.ReadAsStringAsync();
        var initiateResult = JsonSerializer.Deserialize<OAuthAuthorizationResponse>(initiateContent, JsonOptions);
        Assert.NotNull(initiateResult);

        // Now callback with mock code and state
        var callbackRequest = new OAuthCallbackRequest(
            Provider: "mock",
            Code: "mock_auth_code",
            State: initiateResult!.State,
            RedirectUri: "https://localhost/callback");

        _output.WriteLine("[STEP] POST /api/v1/auth/external/callback...");
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/external/callback", callbackRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Should return 201 Created with tokens for new user
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {content}");

        var result = JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.NotEmpty(result!.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.NotNull(result.User);

        _output.WriteLine("[PASS] OAuth callback created new user with session");
    }

    [Fact]
    public async Task OAuthCallback_WithEmptyState_Returns400()
    {
        _output.WriteLine("[TEST] OAuthCallback_WithEmptyState_Returns400");

        var callbackRequest = new OAuthCallbackRequest(
            Provider: "mock",
            Code: "mock_auth_code",
            State: "", // Empty state should fail validation
            RedirectUri: "https://localhost/callback");

        _output.WriteLine("[STEP] POST /api/v1/auth/external/callback with empty state...");
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/external/callback", callbackRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Empty state should fail validation (400 or 401)
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 400 or 401, got {response.StatusCode}");
        _output.WriteLine("[PASS] Returns error for empty state");
    }

    #endregion

    #region Get External Logins Tests

    [Fact]
    public async Task GetExternalLogins_WithoutAuth_Returns401()
    {
        _output.WriteLine("[TEST] GetExternalLogins_WithoutAuth_Returns401");

        var randomUserId = Guid.NewGuid();
        _output.WriteLine($"[STEP] GET /api/v1/auth/users/{randomUserId}/identity/external without token...");
        var response = await _sharedHost.Host.HttpClient.GetAsync($"/api/v1/auth/users/{randomUserId}/identity/external");

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("[PASS] Returns 401 without authentication");
    }

    [Fact]
    public async Task GetExternalLogins_WithAuth_ReturnsEmptyListForNewUser()
    {
        _output.WriteLine("[TEST] GetExternalLogins_WithAuth_ReturnsEmptyListForNewUser");

        // Register a new user with password (no external logins)
        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        _output.WriteLine($"[STEP] GET /api/v1/auth/users/{userId}/identity/external...");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/identity/external");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {content}");

        var result = JsonSerializer.Deserialize<ExternalLoginsResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.Empty(result!.Logins);

        _output.WriteLine("[PASS] Returns empty list for user with no external logins");
    }

    #endregion

    #region Unlink External Login Tests

    [Fact]
    public async Task UnlinkExternalLogin_WithoutAuth_Returns401()
    {
        _output.WriteLine("[TEST] UnlinkExternalLogin_WithoutAuth_Returns401");

        var randomUserId = Guid.NewGuid();
        _output.WriteLine($"[STEP] DELETE /api/v1/auth/users/{randomUserId}/identity/external/mock without token...");
        var response = await _sharedHost.Host.HttpClient.DeleteAsync($"/api/v1/auth/users/{randomUserId}/identity/external/mock");

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("[PASS] Returns 401 without authentication");
    }

    [Fact]
    public async Task UnlinkExternalLogin_NonExistentProvider_Returns404()
    {
        _output.WriteLine("[TEST] UnlinkExternalLogin_NonExistentProvider_Returns404");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        _output.WriteLine($"[STEP] DELETE /api/v1/auth/users/{userId}/identity/external/mock (user has no external logins)...");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/auth/users/{userId}/identity/external/mock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _output.WriteLine("[PASS] Returns 404 for non-existent external login");
    }

    #endregion

    #region Helper Methods

    private async Task<AuthResponse?> RegisterUniqueUserAsync()
    {
        var username = $"oauthtest_{Guid.NewGuid():N}";
        return await RegisterUserAsync(username);
    }

    private async Task<AuthResponse?> RegisterUserAsync(string username)
    {
        var registerRequest = new
        {
            Username = username,
            Email = $"{username}@example.com",
            Password = TestPassword,
            ConfirmPassword = TestPassword
        };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"[ERROR] Registration failed: {errorContent}");
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
    }

    #endregion

    #region Response DTOs

    private record OAuthProvidersResponse(IReadOnlyList<OAuthProviderInfo> Providers);

    private record OAuthProviderInfo(string Provider, string DisplayName, bool IsEnabled, string? IconName);

    private record OAuthLoginRequest(string Provider, string RedirectUri);

    private record OAuthAuthorizationResponse(string AuthorizationUrl, string State);

    private record OAuthCallbackRequest(string Provider, string Code, string State, string RedirectUri);

    private record AuthResponse(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresIn,
        UserInfoResponse User);

    private record UserInfoResponse(
        Guid Id,
        string Username,
        string? Email,
        IReadOnlyCollection<string> Roles,
        IReadOnlyCollection<string> Permissions);

    private record ExternalLoginsResponse(IReadOnlyList<ExternalLoginInfo> Logins);

    private record ExternalLoginInfo(string Provider, string? DisplayName, string? Email, DateTimeOffset LinkedAt);

    #endregion
}
