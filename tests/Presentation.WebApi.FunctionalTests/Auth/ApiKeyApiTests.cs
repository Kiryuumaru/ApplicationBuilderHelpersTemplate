using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Presentation.WebApi.FunctionalTests.Fixtures;

namespace Presentation.WebApi.FunctionalTests.Auth;

/// <summary>
/// Functional tests for API Key Management endpoints.
/// Tests create, list, revoke, and usage of API keys.
/// </summary>
[Collection(WebApiTestCollection.Name)]
public class ApiKeyApiTests
{
    private readonly ITestOutputHelper _output;
    private readonly SharedWebApiHost _sharedHost;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string TestPassword = "TestPassword123!";

    public ApiKeyApiTests(SharedWebApiHost sharedHost, ITestOutputHelper output)
    {
        _sharedHost = sharedHost;
        _output = output;
    }

    #region List API Keys Tests

    [Fact]
    public async Task ListApiKeys_WithoutToken_Returns401()
    {
        _output.WriteLine("[TEST] ListApiKeys_WithoutToken_Returns401");

        var randomUserId = Guid.NewGuid();
        _output.WriteLine($"[STEP] GET /api/v1/auth/users/{randomUserId}/api-keys without token...");
        var response = await _sharedHost.Host.HttpClient.GetAsync($"/api/v1/auth/users/{randomUserId}/api-keys");

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("[PASS] Returns 401 without authentication");
    }

    [Fact]
    public async Task ListApiKeys_AfterRegister_ReturnsEmptyList()
    {
        _output.WriteLine("[TEST] ListApiKeys_AfterRegister_ReturnsEmptyList");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        _output.WriteLine($"[STEP] GET /api/v1/auth/users/{userId}/api-keys with valid token...");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {content}");

        var result = JsonSerializer.Deserialize<ApiKeyListResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.Empty(result!.Items);

        _output.WriteLine("[PASS] List API keys returns empty list for new user");
    }

    [Fact]
    public async Task ListApiKeys_ForOtherUser_Returns403()
    {
        _output.WriteLine("[TEST] ListApiKeys_ForOtherUser_Returns403");

        // Register two users
        var user1 = await RegisterUniqueUserAsync();
        var user2 = await RegisterUniqueUserAsync();
        Assert.NotNull(user1);
        Assert.NotNull(user2);

        // User1 tries to list User2's API keys
        _output.WriteLine($"[STEP] GET /api/v1/auth/users/{user2!.User.Id}/api-keys with User1's token...");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{user2.User.Id}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1!.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] Cannot list another user's API keys");
    }

    #endregion

    #region Create API Key Tests

    [Fact]
    public async Task CreateApiKey_WithValidData_ReturnsCreatedWithKey()
    {
        _output.WriteLine("[TEST] CreateApiKey_WithValidData_ReturnsCreatedWithKey");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        var createRequest = new { Name = "Test API Key" };

        _output.WriteLine($"[STEP] POST /api/v1/auth/users/{userId}/api-keys...");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/users/{userId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {content}");

        var result = JsonSerializer.Deserialize<CreateApiKeyResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result!.Id);
        Assert.Equal("Test API Key", result.Name);
        Assert.False(string.IsNullOrEmpty(result.Key), "API key JWT should be returned");
        Assert.Null(result.ExpiresAt);

        _output.WriteLine("[PASS] API key created successfully with JWT");
    }

    [Fact]
    public async Task CreateApiKey_WithExpiration_ReturnsCreatedWithExpiry()
    {
        _output.WriteLine("[TEST] CreateApiKey_WithExpiration_ReturnsCreatedWithExpiry");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
        var createRequest = new { Name = "Expiring Key", ExpiresAt = expiresAt };

        _output.WriteLine($"[STEP] POST /api/v1/auth/users/{userId}/api-keys with expiration...");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/users/{userId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<CreateApiKeyResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.NotNull(result!.ExpiresAt);

        _output.WriteLine("[PASS] API key created with expiration date");
    }

    [Fact]
    public async Task CreateApiKey_WithPastExpiration_Returns400()
    {
        _output.WriteLine("[TEST] CreateApiKey_WithPastExpiration_Returns400");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        var pastDate = DateTimeOffset.UtcNow.AddDays(-1);
        var createRequest = new { Name = "Past Expiry Key", ExpiresAt = pastDate };

        _output.WriteLine($"[STEP] POST /api/v1/auth/users/{userId}/api-keys with past expiration...");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/users/{userId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _output.WriteLine("[PASS] Rejects API key with past expiration date");
    }

    [Fact]
    public async Task CreateApiKey_WithoutToken_Returns401()
    {
        _output.WriteLine("[TEST] CreateApiKey_WithoutToken_Returns401");

        var randomUserId = Guid.NewGuid();
        var createRequest = new { Name = "Unauthorized Key" };

        _output.WriteLine($"[STEP] POST /api/v1/auth/users/{randomUserId}/api-keys without token...");
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync(
            $"/api/v1/auth/users/{randomUserId}/api-keys", createRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("[PASS] Returns 401 without authentication");
    }

    [Fact]
    public async Task CreateApiKey_ForOtherUser_Returns403()
    {
        _output.WriteLine("[TEST] CreateApiKey_ForOtherUser_Returns403");

        var user1 = await RegisterUniqueUserAsync();
        var user2 = await RegisterUniqueUserAsync();
        Assert.NotNull(user1);
        Assert.NotNull(user2);

        var createRequest = new { Name = "Hacker Key" };

        _output.WriteLine($"[STEP] POST /api/v1/auth/users/{user2!.User.Id}/api-keys with User1's token...");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/users/{user2.User.Id}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1!.AccessToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] Cannot create API key for another user");
    }

    [Fact]
    public async Task CreateApiKey_WithEmptyName_Returns400()
    {
        _output.WriteLine("[TEST] CreateApiKey_WithEmptyName_Returns400");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        var createRequest = new { Name = "" };

        _output.WriteLine($"[STEP] POST /api/v1/auth/users/{userId}/api-keys with empty name...");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/users/{userId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _output.WriteLine("[PASS] Rejects API key with empty name");
    }

    #endregion

    #region Revoke API Key Tests

    [Fact]
    public async Task RevokeApiKey_ExistingKey_Returns204()
    {
        _output.WriteLine("[TEST] RevokeApiKey_ExistingKey_Returns204");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;

        // Create an API key first
        var createResponse = await CreateApiKeyAsync(userId, authResult.AccessToken, "Key to Revoke");
        Assert.NotNull(createResponse);

        _output.WriteLine($"[STEP] DELETE /api/v1/auth/users/{userId}/api-keys/{createResponse!.Id}...");
        using var request = new HttpRequestMessage(HttpMethod.Delete, 
            $"/api/v1/auth/users/{userId}/api-keys/{createResponse.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        _output.WriteLine("[PASS] API key revoked successfully");
    }

    [Fact]
    public async Task RevokeApiKey_NonExistentKey_Returns404()
    {
        _output.WriteLine("[TEST] RevokeApiKey_NonExistentKey_Returns404");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        var nonExistentId = Guid.NewGuid();

        _output.WriteLine($"[STEP] DELETE /api/v1/auth/users/{userId}/api-keys/{nonExistentId}...");
        using var request = new HttpRequestMessage(HttpMethod.Delete, 
            $"/api/v1/auth/users/{userId}/api-keys/{nonExistentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _output.WriteLine("[PASS] Returns 404 for non-existent API key");
    }

    [Fact]
    public async Task RevokeApiKey_AlreadyRevoked_Returns404()
    {
        _output.WriteLine("[TEST] RevokeApiKey_AlreadyRevoked_Returns404");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;

        // Create and revoke an API key
        var createResponse = await CreateApiKeyAsync(userId, authResult.AccessToken, "Double Revoke Key");
        Assert.NotNull(createResponse);

        // First revoke
        using var request1 = new HttpRequestMessage(HttpMethod.Delete, 
            $"/api/v1/auth/users/{userId}/api-keys/{createResponse!.Id}");
        request1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var response1 = await _sharedHost.Host.HttpClient.SendAsync(request1);
        Assert.Equal(HttpStatusCode.NoContent, response1.StatusCode);

        // Second revoke attempt
        _output.WriteLine($"[STEP] DELETE /api/v1/auth/users/{userId}/api-keys/{createResponse.Id} (second time)...");
        using var request2 = new HttpRequestMessage(HttpMethod.Delete, 
            $"/api/v1/auth/users/{userId}/api-keys/{createResponse.Id}");
        request2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var response2 = await _sharedHost.Host.HttpClient.SendAsync(request2);

        _output.WriteLine($"[RECEIVED] Status: {(int)response2.StatusCode} {response2.StatusCode}");

        Assert.Equal(HttpStatusCode.NotFound, response2.StatusCode);
        _output.WriteLine("[PASS] Returns 404 for already revoked API key");
    }

    [Fact]
    public async Task RevokeApiKey_ForOtherUser_Returns403()
    {
        _output.WriteLine("[TEST] RevokeApiKey_ForOtherUser_Returns403");

        var user1 = await RegisterUniqueUserAsync();
        var user2 = await RegisterUniqueUserAsync();
        Assert.NotNull(user1);
        Assert.NotNull(user2);

        // User2 creates an API key
        var createResponse = await CreateApiKeyAsync(user2!.User.Id, user2.AccessToken, "User2 Key");
        Assert.NotNull(createResponse);

        // User1 tries to revoke User2's key
        _output.WriteLine($"[STEP] DELETE /api/v1/auth/users/{user2.User.Id}/api-keys/{createResponse!.Id} with User1's token...");
        using var request = new HttpRequestMessage(HttpMethod.Delete, 
            $"/api/v1/auth/users/{user2.User.Id}/api-keys/{createResponse.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1!.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] Cannot revoke another user's API key");
    }

    #endregion

    #region API Key Usage Tests

    [Fact]
    public async Task UseApiKey_ForRegularEndpoint_ReturnsSuccess()
    {
        _output.WriteLine("[TEST] UseApiKey_ForRegularEndpoint_ReturnsSuccess");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;

        // Create an API key
        var createResponse = await CreateApiKeyAsync(userId, authResult.AccessToken, "Usage Test Key");
        Assert.NotNull(createResponse);

        // Use the API key to access /auth/me
        _output.WriteLine("[STEP] GET /api/v1/auth/me with API key...");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", createResponse!.Key);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var meResult = JsonSerializer.Deserialize<UserInfoResponse>(content, JsonOptions);
        Assert.NotNull(meResult);
        Assert.Equal(userId, meResult!.Id);

        _output.WriteLine("[PASS] API key can access regular endpoints");
    }

    [Fact]
    public async Task UseApiKey_AsRefreshToken_Returns401()
    {
        // API keys are standalone JWTs - they cannot be used as refresh tokens.
        // Submitting an API key JWT as the refresh token should fail.
        _output.WriteLine("[TEST] UseApiKey_AsRefreshToken_Returns401");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;

        // Create an API key
        var createResponse = await CreateApiKeyAsync(userId, authResult.AccessToken, "Not A Refresh Token");
        Assert.NotNull(createResponse);

        // Try to use the API key JWT as a refresh token (should fail)
        _output.WriteLine("[STEP] POST /api/v1/auth/refresh with API key as refresh token...");
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { RefreshToken = createResponse!.Key });

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("[PASS] API key cannot be used as a refresh token");
    }

    [Fact]
    public async Task UseApiKey_ToCreateAnotherApiKey_Returns403()
    {
        _output.WriteLine("[TEST] UseApiKey_ToCreateAnotherApiKey_Returns403");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;

        // Create an API key
        var createResponse = await CreateApiKeyAsync(userId, authResult.AccessToken, "First Key");
        Assert.NotNull(createResponse);

        // Try to create another API key using the first API key (should be denied)
        _output.WriteLine("[STEP] POST /api/v1/auth/users/{userId}/api-keys with API key...");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/users/{userId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", createResponse!.Key);
        request.Content = JsonContent.Create(new { Name = "Second Key via API Key" });
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] API key cannot create other API keys");
    }

    [Fact]
    public async Task UseApiKey_ToListApiKeys_Returns403()
    {
        _output.WriteLine("[TEST] UseApiKey_ToListApiKeys_Returns403");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;

        // Create an API key
        var createResponse = await CreateApiKeyAsync(userId, authResult.AccessToken, "List Deny Key");
        Assert.NotNull(createResponse);

        // Try to list API keys using the API key (should be denied)
        _output.WriteLine("[STEP] GET /api/v1/auth/users/{userId}/api-keys with API key...");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", createResponse!.Key);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] API key cannot list API keys");
    }

    [Fact]
    public async Task UseApiKey_ToRevokeApiKey_Returns403()
    {
        _output.WriteLine("[TEST] UseApiKey_ToRevokeApiKey_Returns403");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;

        // Create two API keys
        var key1 = await CreateApiKeyAsync(userId, authResult.AccessToken, "Key 1");
        var key2 = await CreateApiKeyAsync(userId, authResult.AccessToken, "Key 2");
        Assert.NotNull(key1);
        Assert.NotNull(key2);

        // Try to revoke key2 using key1 (should be denied)
        _output.WriteLine($"[STEP] DELETE /api/v1/auth/users/{userId}/api-keys/{key2!.Id} with API key...");
        using var request = new HttpRequestMessage(HttpMethod.Delete, 
            $"/api/v1/auth/users/{userId}/api-keys/{key2.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key1!.Key);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] API key cannot revoke other API keys");
    }

    [Fact]
    public async Task UseRevokedApiKey_Returns401()
    {
        _output.WriteLine("[TEST] UseRevokedApiKey_Returns401");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;

        // Create an API key
        var createResponse = await CreateApiKeyAsync(userId, authResult.AccessToken, "Soon to be revoked");
        Assert.NotNull(createResponse);

        // Verify it works first
        using var workingRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        workingRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", createResponse!.Key);
        var workingResponse = await _sharedHost.Host.HttpClient.SendAsync(workingRequest);
        Assert.Equal(HttpStatusCode.OK, workingResponse.StatusCode);

        // Revoke the key
        using var revokeRequest = new HttpRequestMessage(HttpMethod.Delete, 
            $"/api/v1/auth/users/{userId}/api-keys/{createResponse.Id}");
        revokeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var revokeResponse = await _sharedHost.Host.HttpClient.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        // Try to use the revoked key
        _output.WriteLine("[STEP] GET /api/v1/auth/me with revoked API key...");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", createResponse.Key);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("[PASS] Revoked API key is rejected");
    }

    #endregion

    #region User Journey Tests

    [Fact]
    public async Task UserJourney_CreateListRevokeApiKey_FullLifecycle()
    {
        _output.WriteLine("[TEST] UserJourney_CreateListRevokeApiKey_FullLifecycle");

        // Step 1: Register a new user
        _output.WriteLine("[STEP 1] Registering new user...");
        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);
        var userId = authResult!.User.Id;

        // Step 2: List API keys (should be empty)
        _output.WriteLine("[STEP 2] Listing API keys (should be empty)...");
        var listBefore = await ListApiKeysAsync(userId, authResult.AccessToken);
        Assert.NotNull(listBefore);
        Assert.Empty(listBefore!.Items);

        // Step 3: Create first API key
        _output.WriteLine("[STEP 3] Creating first API key...");
        var key1 = await CreateApiKeyAsync(userId, authResult.AccessToken, "Production Bot");
        Assert.NotNull(key1);
        var key1Jwt = key1!.Key;

        // Step 4: Create second API key
        _output.WriteLine("[STEP 4] Creating second API key...");
        var key2 = await CreateApiKeyAsync(userId, authResult.AccessToken, "Development Bot");
        Assert.NotNull(key2);

        // Step 5: List API keys (should have 2)
        _output.WriteLine("[STEP 5] Listing API keys (should have 2)...");
        var listAfterCreate = await ListApiKeysAsync(userId, authResult.AccessToken);
        Assert.NotNull(listAfterCreate);
        Assert.Equal(2, listAfterCreate!.Items.Count);

        // Step 6: Use first API key to access an endpoint
        _output.WriteLine("[STEP 6] Using first API key to access /auth/me...");
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key1Jwt);
        var meResponse = await _sharedHost.Host.HttpClient.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        // Step 7: Revoke first API key
        _output.WriteLine("[STEP 7] Revoking first API key...");
        using var revokeRequest = new HttpRequestMessage(HttpMethod.Delete, 
            $"/api/v1/auth/users/{userId}/api-keys/{key1.Id}");
        revokeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var revokeResponse = await _sharedHost.Host.HttpClient.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        // Step 8: Verify revoked key doesn't work
        _output.WriteLine("[STEP 8] Verifying revoked key is rejected...");
        using var rejectedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        rejectedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key1Jwt);
        var rejectedResponse = await _sharedHost.Host.HttpClient.SendAsync(rejectedRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedResponse.StatusCode);

        // Step 9: List API keys (should have 1)
        _output.WriteLine("[STEP 9] Listing API keys (should have 1)...");
        var listAfterRevoke = await ListApiKeysAsync(userId, authResult.AccessToken);
        Assert.NotNull(listAfterRevoke);
        Assert.Single(listAfterRevoke!.Items);
        Assert.Equal("Development Bot", listAfterRevoke.Items[0].Name);

        _output.WriteLine("[PASS] Full API key lifecycle completed successfully");
    }

    [Fact]
    public async Task UserJourney_MultipleApiKeysWithDifferentPermissions()
    {
        _output.WriteLine("[TEST] UserJourney_MultipleApiKeysWithDifferentPermissions");

        // Register user
        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);
        var userId = authResult!.User.Id;

        // Create multiple API keys with different purposes
        var tradingKey = await CreateApiKeyAsync(userId, authResult.AccessToken, "Trading Bot");
        var monitorKey = await CreateApiKeyAsync(userId, authResult.AccessToken, "Monitor Script");
        var cicdKey = await CreateApiKeyAsync(userId, authResult.AccessToken, "CI/CD Pipeline", 
            DateTimeOffset.UtcNow.AddHours(1)); // Short-lived key

        Assert.NotNull(tradingKey);
        Assert.NotNull(monitorKey);
        Assert.NotNull(cicdKey);

        // Verify all keys can access /auth/me
        foreach (var key in new[] { tradingKey!, monitorKey!, cicdKey! })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Key);
            var response = await _sharedHost.Host.HttpClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Verify all keys are listed
        var allKeys = await ListApiKeysAsync(userId, authResult.AccessToken);
        Assert.NotNull(allKeys);
        Assert.Equal(3, allKeys!.Items.Count);

        // Verify expiration is set correctly for CI/CD key
        var cicdKeyInfo = allKeys.Items.FirstOrDefault(k => k.Name == "CI/CD Pipeline");
        Assert.NotNull(cicdKeyInfo);
        Assert.NotNull(cicdKeyInfo!.ExpiresAt);

        _output.WriteLine("[PASS] Multiple API keys managed successfully");
    }

    #endregion

    #region Edge Cases and Stress Tests

    [Fact]
    public async Task CreateApiKey_MultipleKeys_SucceedsUpToLimit()
    {
        // Note: MaxApiKeysPerUser is 100 in ApiKeyService.cs
        // This test creates a few keys and verifies they can be created,
        // but doesn't test the actual 100 limit due to test runtime concerns.
        _output.WriteLine("[TEST] CreateApiKey_MultipleKeys_SucceedsUpToLimit");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        const int testKeyCount = 5;

        // Create several keys to verify the creation works
        for (int i = 1; i <= testKeyCount; i++)
        {
            var response = await CreateApiKeyAsync(userId, authResult.AccessToken, $"Key {i}");
            Assert.NotNull(response);
            _output.WriteLine($"[INFO] Created key {i}/{testKeyCount}");
        }

        // Verify all keys were created
        var allKeys = await ListApiKeysAsync(userId, authResult.AccessToken);
        Assert.NotNull(allKeys);
        Assert.Equal(testKeyCount, allKeys!.Items.Count);

        _output.WriteLine($"[PASS] Successfully created {testKeyCount} API keys (max is 100)");
    }

    [Fact]
    public async Task CreateApiKey_WithVeryLongName_Returns400()
    {
        _output.WriteLine("[TEST] CreateApiKey_WithVeryLongName_Returns400");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        var longName = new string('A', 101); // Over 100 char limit

        _output.WriteLine($"[STEP] POST /api/v1/auth/users/{userId}/api-keys with 101 char name...");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/users/{userId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        request.Content = JsonContent.Create(new { Name = longName });
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _output.WriteLine("[PASS] Rejects API key with name over 100 characters");
    }

    #endregion

    #region Helper Methods

    private async Task<AuthResponse?> RegisterUniqueUserAsync()
    {
        var username = $"apikey_{Guid.NewGuid():N}";
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

    private async Task<CreateApiKeyResponse?> CreateApiKeyAsync(
        Guid userId, 
        string accessToken, 
        string name,
        DateTimeOffset? expiresAt = null)
    {
        var createRequest = expiresAt.HasValue
            ? new { Name = name, ExpiresAt = expiresAt.Value }
            : (object)new { Name = name };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/auth/users/{userId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(createRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"[ERROR] Create API key failed: {errorContent}");
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CreateApiKeyResponse>(content, JsonOptions);
    }

    private async Task<ApiKeyListResponse?> ListApiKeysAsync(Guid userId, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"[ERROR] List API keys failed: {errorContent}");
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiKeyListResponse>(content, JsonOptions);
    }

    #endregion

    #region Response DTOs

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

    private record CreateApiKeyResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = null!;
        public string Key { get; init; } = null!;
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }
    }

    private record ApiKeyInfoResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = null!;
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }
        public DateTimeOffset? LastUsedAt { get; init; }
    }

    private record ApiKeyListResponse(IReadOnlyList<ApiKeyInfoResponse> Items);

    #endregion
}
