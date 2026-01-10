using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Presentation.WebApi.FunctionalTests.Auth;

/// <summary>
/// Deep token misuse tests that strengthen confidence in RBAC-based token separation.
///
/// These tests focus on:
/// - Cross-user header confusion at /auth/refresh (Authorization header vs refresh token in body)
/// - Using refresh tokens as bearer tokens on other protected endpoints
/// </summary>
[Collection(WebApiTestCollection.Name)]
public class RefreshTokenMisuseTests
{
    private readonly ITestOutputHelper _output;
    private readonly SharedWebApiHost _sharedHost;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string TestPassword = "TestPassword123!";

    public RefreshTokenMisuseTests(SharedWebApiHost sharedHost, ITestOutputHelper output)
    {
        _sharedHost = sharedHost;
        _output = output;
    }

    [Fact]
    public async Task Refresh_WithBodyTokenOfUserA_AndAuthorizationHeaderOfUserB_IssuesTokensForUserA()
    {
        _output.WriteLine("[TEST] Refresh_WithBodyTokenOfUserA_AndAuthorizationHeaderOfUserB_IssuesTokensForUserA");

        // Arrange: two distinct users
        var userA = await RegisterUniqueUserAsync();
        Assert.NotNull(userA);
        Assert.NotNull(userA!.User);

        var userB = await RegisterUniqueUserAsync();
        Assert.NotNull(userB);

        // Act: call /auth/refresh with A's refresh token in body, but B's access token in Authorization header
        using var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userB!.AccessToken);
        refreshReq.Content = JsonContent.Create(new { RefreshToken = userA.RefreshToken });

        var refreshResp = await _sharedHost.Host.HttpClient.SendAsync(refreshReq);
        _output.WriteLine($"[RECEIVED] Refresh status: {(int)refreshResp.StatusCode} {refreshResp.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);

        var refreshContent = await refreshResp.Content.ReadAsStringAsync();
        var refreshed = JsonSerializer.Deserialize<AuthResponse>(refreshContent, JsonOptions);
        Assert.NotNull(refreshed);
        Assert.False(string.IsNullOrWhiteSpace(refreshed!.AccessToken));

        // Assert: the issued access token belongs to user A (prove via /auth/me)
        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);

        var meResp = await _sharedHost.Host.HttpClient.SendAsync(meReq);
        _output.WriteLine($"[RECEIVED] /me status: {(int)meResp.StatusCode} {meResp.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);

        var meContent = await meResp.Content.ReadAsStringAsync();
        var me = JsonSerializer.Deserialize<MeResponse>(meContent, JsonOptions);
        Assert.NotNull(me);
        Assert.Equal(userA.User!.Id, me!.Id);

        _output.WriteLine("[PASS] Refresh uses body token identity, not Authorization header");
    }

    [Fact]
    public async Task RefreshToken_AsBearerToken_CannotAccessUserSessions_Returns403()
    {
        _output.WriteLine("[TEST] RefreshToken_AsBearerToken_CannotAccessUserSessions_Returns403");

        var auth = await RegisterUniqueUserAsync();
        Assert.NotNull(auth);
        Assert.NotNull(auth!.User);

        // Act: use refresh token as bearer token on sessions endpoint
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{auth.User!.Id}/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.RefreshToken);

        var response = await _sharedHost.Host.HttpClient.SendAsync(request);
        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] Refresh token cannot be used as bearer token for sessions");
    }

    private async Task<AuthResponse?> RegisterUniqueUserAsync()
    {
        var username = $"user_{Guid.NewGuid():N}";

        var registerRequest = new
        {
            Username = username,
            Password = TestPassword,
            ConfirmPassword = TestPassword,
            Email = $"{username}@test.com"
        };

        var registerResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        if (registerResponse.StatusCode == HttpStatusCode.Conflict)
        {
            return await LoginAsync(username, TestPassword);
        }

        if (!registerResponse.IsSuccessStatusCode)
        {
            var error = await registerResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"[ERROR] Registration failed: {error}");
            return null;
        }

        var content = await registerResponse.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
    }

    private async Task<AuthResponse?> LoginAsync(string username, string password)
    {
        var loginRequest = new { Username = username, Password = password };

        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"[ERROR] Login failed: {error}");
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
    }

    private sealed class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public UserInfo? User { get; set; }
    }

    private sealed class UserInfo
    {
        public Guid Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public IReadOnlyCollection<string>? Roles { get; set; }
        public IReadOnlyCollection<string>? Permissions { get; set; }
        public bool IsAnonymous { get; set; }
    }

    private sealed class MeResponse
    {
        public Guid Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public IReadOnlyCollection<string>? Roles { get; set; }
        public IReadOnlyCollection<string>? Permissions { get; set; }
        public bool IsAnonymous { get; set; }
    }
}
