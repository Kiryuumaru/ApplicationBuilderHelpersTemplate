using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Presentation.WebApi.FunctionalTests.Fixtures;

namespace Presentation.WebApi.FunctionalTests.Auth;

/// <summary>
/// Functional tests for Session Management API endpoints.
/// Tests session listing, revocation, and refresh token rotation.
/// </summary>
[Collection(WebApiTestCollection.Name)]
public class SessionApiTests
{
    private readonly ITestOutputHelper _output;
    private readonly SharedWebApiHost _sharedHost;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string TestPassword = "TestPassword123!";

    public SessionApiTests(SharedWebApiHost sharedHost, ITestOutputHelper output)
    {
        _sharedHost = sharedHost;
        _output = output;
    }

    #region List Sessions Tests

    [Fact]
    public async Task ListSessions_AfterRegister_ReturnsOneSession()
    {
        _output.WriteLine("[TEST] ListSessions_AfterRegister_ReturnsOneSession");

        // Register a new user
        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        _output.WriteLine($"[STEP] GET /api/v1/auth/users/{userId}/sessions with valid token...");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {content}");

        var result = JsonSerializer.Deserialize<SessionListResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.True(result.Items[0].IsCurrent, "The only session should be marked as current");

        _output.WriteLine("[PASS] List sessions returned one current session");
    }

    [Fact]
    public async Task ListSessions_WithoutToken_Returns401()
    {
        _output.WriteLine("[TEST] ListSessions_WithoutToken_Returns401");

        // Use a random userId - should still return 401 without token
        var randomUserId = Guid.NewGuid();
        _output.WriteLine($"[STEP] GET /api/v1/auth/users/{randomUserId}/sessions without token...");
        var response = await _sharedHost.Host.HttpClient.GetAsync($"/api/v1/auth/users/{randomUserId}/sessions");

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("[PASS] Returns 401 without authentication");
    }

    [Fact]
    public async Task ListSessions_AfterMultipleLogins_ReturnsMultipleSessions()
    {
        _output.WriteLine("[TEST] ListSessions_AfterMultipleLogins_ReturnsMultipleSessions");

        var username = $"multisession_{Guid.NewGuid():N}";

        // Register user
        var registerResult = await RegisterUserAsync(username);
        Assert.NotNull(registerResult);

        // Login again (creates second session)
        var loginResult = await LoginUserAsync(username);
        Assert.NotNull(loginResult);

        var userId = loginResult!.User.Id;
        _output.WriteLine($"[STEP] GET /api/v1/auth/users/{userId}/sessions...");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {content}");

        var result = JsonSerializer.Deserialize<SessionListResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(2, result!.Items.Count);
        Assert.Single(result.Items, s => s.IsCurrent);

        _output.WriteLine("[PASS] List sessions returned multiple sessions with one current");
    }

    #endregion

    #region Revoke Session Tests

    [Fact]
    public async Task RevokeSession_CurrentSession_ReturnsNoContent()
    {
        _output.WriteLine("[TEST] RevokeSession_CurrentSession_ReturnsNoContent");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;

        // Get the current session ID
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/sessions");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var listResponse = await _sharedHost.Host.HttpClient.SendAsync(listRequest);
        var listContent = await listResponse.Content.ReadAsStringAsync();
        var sessions = JsonSerializer.Deserialize<SessionListResponse>(listContent, JsonOptions);
        var currentSession = sessions!.Items.First(s => s.IsCurrent);

        _output.WriteLine($"[STEP] DELETE /api/v1/auth/users/{userId}/sessions/{currentSession.Id} (current session)...");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/auth/users/{userId}/sessions/{currentSession.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        _output.WriteLine("[PASS] Returns 204 when revoking current session (now allowed)");
    }

    [Fact]
    public async Task RevokeSession_OtherSession_ReturnsNoContent()
    {
        _output.WriteLine("[TEST] RevokeSession_OtherSession_ReturnsNoContent");

        var username = $"revokesession_{Guid.NewGuid():N}";

        // Register and then login again to create two sessions
        var firstAuth = await RegisterUserAsync(username);
        Assert.NotNull(firstAuth);

        var secondAuth = await LoginUserAsync(username);
        Assert.NotNull(secondAuth);

        var userId = secondAuth!.User.Id;

        // Get the first session ID (not current from perspective of second login)
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/sessions");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondAuth.AccessToken);
        var listResponse = await _sharedHost.Host.HttpClient.SendAsync(listRequest);
        var listContent = await listResponse.Content.ReadAsStringAsync();
        var sessions = JsonSerializer.Deserialize<SessionListResponse>(listContent, JsonOptions);
        var otherSession = sessions!.Items.First(s => !s.IsCurrent);

        _output.WriteLine($"[STEP] DELETE /api/v1/auth/users/{userId}/sessions/{otherSession.Id} (other session)...");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/auth/users/{userId}/sessions/{otherSession.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondAuth.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify session count is now 1
        using var verifyRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/sessions");
        verifyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondAuth.AccessToken);
        var verifyResponse = await _sharedHost.Host.HttpClient.SendAsync(verifyRequest);
        var verifyContent = await verifyResponse.Content.ReadAsStringAsync();
        var remainingSessions = JsonSerializer.Deserialize<SessionListResponse>(verifyContent, JsonOptions);

        Assert.Single(remainingSessions!.Items);
        _output.WriteLine("[PASS] Revoked other session successfully");
    }

    [Fact]
    public async Task RevokeSession_NonExistentId_Returns404()
    {
        _output.WriteLine("[TEST] RevokeSession_NonExistentId_Returns404");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var userId = authResult!.User.Id;
        var fakeSessionId = Guid.NewGuid();

        _output.WriteLine($"[STEP] DELETE /api/v1/auth/users/{userId}/sessions/{fakeSessionId} (non-existent)...");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/auth/users/{userId}/sessions/{fakeSessionId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _output.WriteLine("[PASS] Returns 404 for non-existent session");
    }

    #endregion

    #region Revoke All Sessions Tests

    [Fact]
    public async Task RevokeAllSessions_WithMultipleSessions_RevokesAll()
    {
        _output.WriteLine("[TEST] RevokeAllSessions_WithMultipleSessions_RevokesAll");

        var username = $"revokeall_{Guid.NewGuid():N}";

        // Create multiple sessions
        await RegisterUserAsync(username);
        await LoginUserAsync(username);
        var currentAuth = await LoginUserAsync(username);
        Assert.NotNull(currentAuth);

        var userId = currentAuth!.User.Id;

        // Verify 3 sessions exist
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/sessions");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentAuth.AccessToken);
        var listResponse = await _sharedHost.Host.HttpClient.SendAsync(listRequest);
        var listContent = await listResponse.Content.ReadAsStringAsync();
        var initialSessions = JsonSerializer.Deserialize<SessionListResponse>(listContent, JsonOptions);
        Assert.Equal(3, initialSessions!.Items.Count);

        _output.WriteLine($"[STEP] DELETE /api/v1/auth/users/{userId}/sessions (revoke all)...");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/auth/users/{userId}/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentAuth.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {content}");

        var result = JsonSerializer.Deserialize<SessionRevokeAllResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(3, result!.RevokedCount); // All sessions are revoked including current

        // After revoking all sessions, the access token should no longer work
        using var verifyRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/sessions");
        verifyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentAuth.AccessToken);
        var verifyResponse = await _sharedHost.Host.HttpClient.SendAsync(verifyRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);

        _output.WriteLine("[PASS] Revoked all sessions including current");
    }

    #endregion

    #region Refresh Token Rotation Tests

    [Fact]
    public async Task RefreshToken_RotatesToken_OldTokenInvalid()
    {
        _output.WriteLine("[TEST] RefreshToken_RotatesToken_OldTokenInvalid");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var originalRefreshToken = authResult!.RefreshToken;

        // First refresh - should succeed
        _output.WriteLine("[STEP] POST /api/v1/auth/refresh with original token...");
        var refreshRequest = new { RefreshToken = originalRefreshToken };
        var firstRefreshResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)firstRefreshResponse.StatusCode} {firstRefreshResponse.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);

        var firstRefreshContent = await firstRefreshResponse.Content.ReadAsStringAsync();
        var newTokens = JsonSerializer.Deserialize<AuthResponse>(firstRefreshContent, JsonOptions);
        Assert.NotNull(newTokens);
        Assert.NotEqual(originalRefreshToken, newTokens!.RefreshToken);

        _output.WriteLine($"[INFO] Old refresh token: {originalRefreshToken[..20]}...");
        _output.WriteLine($"[INFO] New refresh token: {newTokens.RefreshToken[..20]}...");

        // Second refresh with OLD token - should fail (token rotation)
        _output.WriteLine("[STEP] POST /api/v1/auth/refresh with OLD token (should fail)...");
        var oldTokenRequest = new { RefreshToken = originalRefreshToken };
        var secondRefreshResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", oldTokenRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)secondRefreshResponse.StatusCode} {secondRefreshResponse.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, secondRefreshResponse.StatusCode);

        _output.WriteLine("[PASS] Old refresh token is invalid after rotation");
    }

    [Fact]
    public async Task RefreshToken_WithNewToken_Succeeds()
    {
        _output.WriteLine("[TEST] RefreshToken_WithNewToken_Succeeds");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        // First refresh
        var refreshRequest1 = new { RefreshToken = authResult!.RefreshToken };
        var response1 = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var content1 = await response1.Content.ReadAsStringAsync();
        var tokens1 = JsonSerializer.Deserialize<AuthResponse>(content1, JsonOptions);

        // Second refresh with the NEW token
        _output.WriteLine("[STEP] POST /api/v1/auth/refresh with rotated token...");
        var refreshRequest2 = new { RefreshToken = tokens1!.RefreshToken };
        var response2 = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest2);

        _output.WriteLine($"[RECEIVED] Status: {(int)response2.StatusCode} {response2.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var content2 = await response2.Content.ReadAsStringAsync();
        var tokens2 = JsonSerializer.Deserialize<AuthResponse>(content2, JsonOptions);
        Assert.NotNull(tokens2);
        Assert.NotEqual(tokens1.RefreshToken, tokens2!.RefreshToken);

        _output.WriteLine("[PASS] Refresh with rotated token succeeds");
    }

    #endregion

    #region Logout Revokes Session Tests

    [Fact]
    public async Task Logout_RevokesCurrentSession()
    {
        _output.WriteLine("[TEST] Logout_RevokesCurrentSession");

        var username = $"logouttest_{Guid.NewGuid():N}";

        // Create two sessions
        await RegisterUserAsync(username);
        var secondAuth = await LoginUserAsync(username);
        Assert.NotNull(secondAuth);

        // Logout from second session
        _output.WriteLine("[STEP] POST /api/v1/auth/logout...");
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondAuth!.AccessToken);
        var logoutResponse = await _sharedHost.Host.HttpClient.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        // Refresh token should now be invalid for logged out session
        _output.WriteLine("[STEP] POST /api/v1/auth/refresh after logout (should fail)...");
        var refreshRequest = new { RefreshToken = secondAuth.RefreshToken };
        var refreshResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)refreshResponse.StatusCode} {refreshResponse.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);

        _output.WriteLine("[PASS] Logout invalidates session's refresh token");
    }

    #endregion

    #region Helper Methods

    private async Task<AuthResponse?> RegisterUniqueUserAsync()
    {
        var username = $"sessiontest_{Guid.NewGuid():N}";
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

    private async Task<AuthResponse?> LoginUserAsync(string username)
    {
        var loginRequest = new { Username = username, Password = TestPassword };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"[ERROR] Login failed: {errorContent}");
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
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

    private record SessionInfoResponse(
        Guid Id,
        string? DeviceName,
        string? IpAddress,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastUsedAt,
        bool IsCurrent);

    private record SessionListResponse(
        IReadOnlyList<SessionInfoResponse> Items);

    private record SessionRevokeAllResponse(
        int RevokedCount);

    #endregion
}
