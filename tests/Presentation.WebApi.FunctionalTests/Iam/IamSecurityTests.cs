using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Presentation.WebApi.FunctionalTests.Fixtures;

namespace Presentation.WebApi.FunctionalTests.Iam;

/// <summary>
/// Security tests for IAM API endpoints.
/// Tests access control, self-escalation prevention, and cross-user access restrictions.
/// </summary>
[Collection(WebApiTestCollection.Name)]
public class IamSecurityTests
{
    private readonly ITestOutputHelper _output;
    private readonly SharedWebApiHost _sharedHost;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string TestPassword = "TestPassword123!";

    public IamSecurityTests(SharedWebApiHost sharedHost, ITestOutputHelper output)
    {
        _sharedHost = sharedHost;
        _output = output;
    }

    #region Self-Escalation Prevention Tests

    [Fact]
    public async Task User_CannotAssignRoleToSelf()
    {
        _output.WriteLine("[TEST] User_CannotAssignRoleToSelf");

        // Register a user
        var userAuth = await RegisterAndGetTokenAsync();
        Assert.NotNull(userAuth);
        var userId = userAuth!.User!.Id;

        // Attempt to assign ADMIN role to self
        var roleRequest = new { UserId = userId, RoleCode = "ADMIN" };

        _output.WriteLine($"[STEP] User {userId} attempting to assign ADMIN role to themselves...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/assign");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);
        request.Content = JsonContent.Create(roleRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Should be forbidden - users can't assign roles to themselves
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] User cannot assign role to themselves");
    }

    [Fact]
    public async Task User_CannotRemoveRoleFromSelf()
    {
        _output.WriteLine("[TEST] User_CannotRemoveRoleFromSelf");

        var userAuth = await RegisterAndGetTokenAsync();
        Assert.NotNull(userAuth);
        var userId = userAuth!.User!.Id;

        // User role ID (standard user role)
        var userRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var roleRequest = new { UserId = userId, RoleId = userRoleId };

        _output.WriteLine($"[STEP] User {userId} attempting to remove USER role from themselves...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/remove");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);
        request.Content = JsonContent.Create(roleRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Should be forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] User cannot remove role from themselves");
    }

    [Fact]
    public async Task User_CannotGrantPermissionToSelf()
    {
        _output.WriteLine("[TEST] User_CannotGrantPermissionToSelf");

        var userAuth = await RegisterAndGetTokenAsync();
        Assert.NotNull(userAuth);
        var userId = userAuth!.User!.Id;

        // Attempt to grant admin permission to self
        var grantRequest = new
        {
            UserId = userId,
            PermissionIdentifier = "api:iam:users:_write",
            Description = "Self-escalation attempt"
        };

        _output.WriteLine($"[STEP] User {userId} attempting to grant admin permission to themselves...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/permissions/grant");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);
        request.Content = JsonContent.Create(grantRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Should be forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] User cannot grant permission to themselves");
    }

    [Fact]
    public async Task User_CannotRevokePermissionFromSelf()
    {
        _output.WriteLine("[TEST] User_CannotRevokePermissionFromSelf");

        var userAuth = await RegisterAndGetTokenAsync();
        Assert.NotNull(userAuth);
        var userId = userAuth!.User!.Id;

        var revokeRequest = new
        {
            UserId = userId,
            PermissionIdentifier = "api:auth:me:_write"
        };

        _output.WriteLine($"[STEP] User {userId} attempting to revoke permission from themselves...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/permissions/revoke");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);
        request.Content = JsonContent.Create(revokeRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Should be forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] User cannot revoke permission from themselves");
    }

    #endregion

    #region Cross-User Access Prevention Tests

    [Fact]
    public async Task User_CannotAccessOtherUserInfo()
    {
        _output.WriteLine("[TEST] User_CannotAccessOtherUserInfo");

        // Register two users
        var user1Auth = await RegisterAndGetTokenAsync();
        var user2Auth = await RegisterAndGetTokenAsync();
        Assert.NotNull(user1Auth);
        Assert.NotNull(user2Auth);

        var user2Id = user2Auth!.User!.Id;

        _output.WriteLine($"[STEP] User 1 attempting to access User 2's info ({user2Id})...");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{user2Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Auth!.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Should be forbidden - users can't access other users' info
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] User cannot access other user's info");
    }

    [Fact]
    public async Task User_CannotUpdateOtherUser()
    {
        _output.WriteLine("[TEST] User_CannotUpdateOtherUser");

        var user1Auth = await RegisterAndGetTokenAsync();
        var user2Auth = await RegisterAndGetTokenAsync();
        Assert.NotNull(user1Auth);
        Assert.NotNull(user2Auth);

        var user2Id = user2Auth!.User!.Id;
        var updateRequest = new { Email = "hacked@evil.com" };

        _output.WriteLine($"[STEP] User 1 attempting to update User 2's profile ({user2Id})...");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/iam/users/{user2Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Auth!.AccessToken);
        request.Content = JsonContent.Create(updateRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Should be forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] User cannot update other user's profile");
    }

    [Fact]
    public async Task User_CannotDeleteOtherUser()
    {
        _output.WriteLine("[TEST] User_CannotDeleteOtherUser");

        var user1Auth = await RegisterAndGetTokenAsync();
        var user2Auth = await RegisterAndGetTokenAsync();
        Assert.NotNull(user1Auth);
        Assert.NotNull(user2Auth);

        var user2Id = user2Auth!.User!.Id;

        _output.WriteLine($"[STEP] User 1 attempting to delete User 2 ({user2Id})...");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/iam/users/{user2Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Auth!.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Should be forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] User cannot delete other user");
    }

    [Fact]
    public async Task User_CannotViewOtherUserPermissions()
    {
        _output.WriteLine("[TEST] User_CannotViewOtherUserPermissions");

        var user1Auth = await RegisterAndGetTokenAsync();
        var user2Auth = await RegisterAndGetTokenAsync();
        Assert.NotNull(user1Auth);
        Assert.NotNull(user2Auth);

        var user2Id = user2Auth!.User!.Id;

        _output.WriteLine($"[STEP] User 1 attempting to view User 2's permissions ({user2Id})...");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{user2Id}/permissions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Auth!.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Should be forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] User cannot view other user's permissions");
    }

    [Fact]
    public async Task User_CannotAssignRoleToOtherUser()
    {
        _output.WriteLine("[TEST] User_CannotAssignRoleToOtherUser");

        var user1Auth = await RegisterAndGetTokenAsync();
        var user2Auth = await RegisterAndGetTokenAsync();
        Assert.NotNull(user1Auth);
        Assert.NotNull(user2Auth);

        var user2Id = user2Auth!.User!.Id;
        var roleRequest = new { UserId = user2Id, RoleCode = "ADMIN" };

        _output.WriteLine($"[STEP] User 1 attempting to assign ADMIN role to User 2 ({user2Id})...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/assign");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Auth!.AccessToken);
        request.Content = JsonContent.Create(roleRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Should be forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] User cannot assign role to other user");
    }

    [Fact]
    public async Task User_CannotGrantPermissionToOtherUser()
    {
        _output.WriteLine("[TEST] User_CannotGrantPermissionToOtherUser");

        var user1Auth = await RegisterAndGetTokenAsync();
        var user2Auth = await RegisterAndGetTokenAsync();
        Assert.NotNull(user1Auth);
        Assert.NotNull(user2Auth);

        var user2Id = user2Auth!.User!.Id;
        var grantRequest = new
        {
            UserId = user2Id,
            PermissionIdentifier = "api:iam:users:_write",
            Description = "Unauthorized grant attempt"
        };

        _output.WriteLine($"[STEP] User 1 attempting to grant permission to User 2 ({user2Id})...");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/permissions/grant");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1Auth!.AccessToken);
        request.Content = JsonContent.Create(grantRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Should be forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] User cannot grant permission to other user");
    }

    #endregion

    #region IAM Endpoint Access Control Tests

    [Fact]
    public async Task UnauthenticatedUser_CannotAccessIamEndpoints()
    {
        _output.WriteLine("[TEST] UnauthenticatedUser_CannotAccessIamEndpoints");

        var endpoints = new[]
        {
            ("GET", "/api/v1/iam/users"),
            ("GET", $"/api/v1/iam/users/{Guid.NewGuid()}"),
            ("PUT", $"/api/v1/iam/users/{Guid.NewGuid()}"),
            ("DELETE", $"/api/v1/iam/users/{Guid.NewGuid()}"),
            ("GET", $"/api/v1/iam/users/{Guid.NewGuid()}/permissions"),
            ("POST", "/api/v1/iam/roles/assign"),
            ("POST", "/api/v1/iam/roles/remove"),
            ("POST", "/api/v1/iam/permissions/grant"),
            ("POST", "/api/v1/iam/permissions/revoke")
        };

        foreach (var (method, endpoint) in endpoints)
        {
            _output.WriteLine($"[STEP] {method} {endpoint} without authentication...");
            using var request = new HttpRequestMessage(new HttpMethod(method), endpoint);

            // Add content for POST/PUT requests
            if (method is "POST" or "PUT")
            {
                request.Content = JsonContent.Create(new { });
            }

            var response = await _sharedHost.Host.HttpClient.SendAsync(request);

            _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        _output.WriteLine("[PASS] All IAM endpoints require authentication");
    }

    [Fact]
    public async Task RegularUser_CannotListAllUsers()
    {
        _output.WriteLine("[TEST] RegularUser_CannotListAllUsers");

        var userAuth = await RegisterAndGetTokenAsync();
        Assert.NotNull(userAuth);

        _output.WriteLine("[STEP] GET /api/v1/iam/users as regular user...");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/iam/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth!.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Regular users don't have api:iam:users:list permission
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _output.WriteLine("[PASS] Regular user cannot list all users");
    }

    [Fact]
    public async Task RegularUser_CanAccessOwnInfo()
    {
        _output.WriteLine("[TEST] RegularUser_CanAccessOwnInfo");

        var userAuth = await RegisterAndGetTokenAsync();
        Assert.NotNull(userAuth);
        var userId = userAuth!.User!.Id;

        _output.WriteLine($"[STEP] GET /api/v1/iam/users/{userId} as the owner...");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Users should be able to access their own info via userId-scoped permission
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UserResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(userId, result!.Id);

        _output.WriteLine("[PASS] User can access their own info");
    }

    [Fact]
    public async Task RegularUser_CanViewOwnPermissions()
    {
        _output.WriteLine("[TEST] RegularUser_CanViewOwnPermissions");

        var userAuth = await RegisterAndGetTokenAsync();
        Assert.NotNull(userAuth);
        var userId = userAuth!.User!.Id;

        _output.WriteLine($"[STEP] GET /api/v1/iam/users/{userId}/permissions as the owner...");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{userId}/permissions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Users should be able to view their own permissions
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {content}");

        var result = JsonSerializer.Deserialize<PermissionsResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(userId, result!.UserId);

        _output.WriteLine("[PASS] User can view their own permissions");
    }

    [Fact]
    public async Task RegularUser_CanUpdateOwnProfile()
    {
        _output.WriteLine("[TEST] RegularUser_CanUpdateOwnProfile");

        var userAuth = await RegisterAndGetTokenAsync();
        Assert.NotNull(userAuth);
        var userId = userAuth!.User!.Id;
        var newEmail = $"updated_{Guid.NewGuid():N}@test.com";

        var updateRequest = new { Email = newEmail };

        _output.WriteLine($"[STEP] PUT /api/v1/iam/users/{userId} to update own email...");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/iam/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);
        request.Content = JsonContent.Create(updateRequest);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Users should be able to update their own profile
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UserResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(newEmail, result!.Email);

        _output.WriteLine("[PASS] User can update their own profile");
    }

    #endregion

    #region Privilege Escalation Attempt Tests

    [Fact]
    public async Task User_CannotEscalateViaRoleAssignment()
    {
        _output.WriteLine("[TEST] User_CannotEscalateViaRoleAssignment");

        var userAuth = await RegisterAndGetTokenAsync();
        Assert.NotNull(userAuth);
        var userId = userAuth!.User!.Id;

        // Try multiple admin-level roles
        var adminRoles = new[] { "ADMIN", "SUPER_ADMIN", "SYSTEM", "ROOT" };

        foreach (var roleCode in adminRoles)
        {
            var roleRequest = new { UserId = userId, RoleCode = roleCode };

            _output.WriteLine($"[STEP] Attempting to assign {roleCode} role to self...");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/assign");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);
            request.Content = JsonContent.Create(roleRequest);
            var response = await _sharedHost.Host.HttpClient.SendAsync(request);

            _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        _output.WriteLine("[PASS] User cannot escalate privileges via role assignment");
    }

    [Fact]
    public async Task User_CannotEscalateViaPermissionGrant()
    {
        _output.WriteLine("[TEST] User_CannotEscalateViaPermissionGrant");

        var userAuth = await RegisterAndGetTokenAsync();
        Assert.NotNull(userAuth);
        var userId = userAuth!.User!.Id;

        // Try granting admin-level permissions
        var adminPermissions = new[]
        {
            "api:iam:users:_write",
            "api:iam:roles:assign",
            "api:iam:permissions:grant",
            "*",
            "api:*"
        };

        foreach (var permission in adminPermissions)
        {
            var grantRequest = new
            {
                UserId = userId,
                PermissionIdentifier = permission,
                Description = "Escalation attempt"
            };

            _output.WriteLine($"[STEP] Attempting to grant '{permission}' to self...");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/permissions/grant");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);
            request.Content = JsonContent.Create(grantRequest);
            var response = await _sharedHost.Host.HttpClient.SendAsync(request);

            _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        _output.WriteLine("[PASS] User cannot escalate privileges via permission grant");
    }

    #endregion

    #region Helper Methods

    private async Task<AuthResponse?> RegisterAndGetTokenAsync()
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
            var loginReq = new { Username = username, Password = TestPassword };
            registerResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginReq);
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

    #endregion

    #region Response Types

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
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public IReadOnlyCollection<string>? Roles { get; set; }
        public IReadOnlyCollection<string>? Permissions { get; set; }
    }

    private sealed class UserResponse
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? PhoneNumber { get; set; }
        public bool PhoneNumberConfirmed { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public bool LockoutEnabled { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public int AccessFailedCount { get; set; }
        public DateTimeOffset Created { get; set; }
        public IReadOnlyCollection<Guid> RoleIds { get; set; } = [];
    }

    private sealed class PermissionsResponse
    {
        public Guid UserId { get; set; }
        public IReadOnlyCollection<string> Permissions { get; set; } = [];
    }

    #endregion
}
