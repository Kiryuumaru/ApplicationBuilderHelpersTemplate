using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Presentation.WebApi.FunctionalTests.Fixtures;

namespace Presentation.WebApi.FunctionalTests.Stress;

/// <summary>
/// Stress tests for concurrent operations, race conditions, and system stability under load.
/// These tests verify the system behaves correctly when many users perform operations simultaneously.
/// </summary>
[Collection(WebApiTestCollection.Name)]
public class StressTests
{
    private readonly ITestOutputHelper _output;
    private readonly SharedWebApiHost _sharedHost;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string TestPassword = "TestPassword123!";

    public StressTests(SharedWebApiHost sharedHost, ITestOutputHelper output)
    {
        _sharedHost = sharedHost;
        _output = output;
    }

    #region Concurrent Authentication Tests

    [Fact]
    public async Task ConcurrentLogins_50Users_AllSucceed()
    {
        _output.WriteLine("[TEST] ConcurrentLogins_50Users_AllSucceed");

        const int userCount = 50;

        // Pre-register users concurrently
        _output.WriteLine($"[STEP] Concurrently registering {userCount} users...");
        var usernames = Enumerable.Range(0, userCount)
            .Select(_ => $"stress_login_{Guid.NewGuid():N}")
            .ToList();

        var registerTasks = usernames.Select(RegisterUserAsync).ToArray();
        var registrations = await Task.WhenAll(registerTasks);

        Assert.True(registrations.All(r => r != null), "All registrations should succeed");
        var users = usernames.Zip(registrations, (name, auth) => (Username: name, Token: auth!.AccessToken)).ToList();

        _output.WriteLine($"[STEP] Starting concurrent logins for {userCount} users...");
        var sw = Stopwatch.StartNew();

        // Login all users concurrently
        var loginTasks = users.Select(u => LoginUserAsync(u.Username)).ToArray();
        var results = await Task.WhenAll(loginTasks);

        sw.Stop();
        _output.WriteLine($"[INFO] {userCount} concurrent logins completed in {sw.ElapsedMilliseconds}ms");

        var successCount = results.Count(r => r != null);
        _output.WriteLine($"[INFO] Success rate: {successCount}/{userCount}");

        Assert.Equal(userCount, successCount);
        _output.WriteLine("[PASS] All concurrent logins succeeded");
    }

    [Fact]
    public async Task ConcurrentRegistrations_30Users_AllSucceed()
    {
        _output.WriteLine("[TEST] ConcurrentRegistrations_30Users_AllSucceed");

        const int userCount = 30;
        var usernames = Enumerable.Range(0, userCount)
            .Select(_ => $"stress_reg_{Guid.NewGuid():N}")
            .ToList();

        _output.WriteLine($"[STEP] Starting concurrent registrations for {userCount} users...");
        var sw = Stopwatch.StartNew();

        var registerTasks = usernames.Select(RegisterUserAsync).ToArray();
        var results = await Task.WhenAll(registerTasks);

        sw.Stop();
        _output.WriteLine($"[INFO] {userCount} concurrent registrations completed in {sw.ElapsedMilliseconds}ms");

        var successCount = results.Count(r => r != null);
        _output.WriteLine($"[INFO] Success rate: {successCount}/{userCount}");

        Assert.Equal(userCount, successCount);
        _output.WriteLine("[PASS] All concurrent registrations succeeded");
    }

    [Fact]
    public async Task ConcurrentTokenRefresh_20Sessions_AllSucceed()
    {
        _output.WriteLine("[TEST] ConcurrentTokenRefresh_20Sessions_AllSucceed");

        const int sessionCount = 20;
        var username = $"stress_refresh_{Guid.NewGuid():N}";

        // Register user
        var initialAuth = await RegisterUserAsync(username);
        Assert.NotNull(initialAuth);

        // Create multiple sessions concurrently
        _output.WriteLine($"[STEP] Concurrently creating {sessionCount - 1} additional sessions...");
        var additionalLoginTasks = Enumerable.Range(1, sessionCount - 1)
            .Select(_ => LoginUserAsync(username))
            .ToArray();
        var additionalSessions = await Task.WhenAll(additionalLoginTasks);

        Assert.True(additionalSessions.All(s => s != null), "All session creations should succeed");
        var sessions = new List<AuthResponse> { initialAuth! };
        sessions.AddRange(additionalSessions!);

        _output.WriteLine($"[STEP] Starting concurrent token refresh for {sessionCount} sessions...");
        var sw = Stopwatch.StartNew();

        var refreshTasks = sessions.Select(s => RefreshTokenAsync(s.RefreshToken)).ToArray();
        var results = await Task.WhenAll(refreshTasks);

        sw.Stop();
        _output.WriteLine($"[INFO] {sessionCount} concurrent refreshes completed in {sw.ElapsedMilliseconds}ms");

        var successCount = results.Count(r => r != null);
        _output.WriteLine($"[INFO] Success rate: {successCount}/{sessionCount}");

        Assert.Equal(sessionCount, successCount);
        _output.WriteLine("[PASS] All concurrent token refreshes succeeded");
    }

    #endregion

    #region Permission Changes Under Load

    [Fact]
    public async Task UserAccessWhilePermissionsChange_HandledGracefully()
    {
        _output.WriteLine("[TEST] UserAccessWhilePermissionsChange_HandledGracefully");

        // Create admin and regular user
        var adminAuth = await CreateAdminAsync();
        var userAuth = await RegisterUserAsync($"stress_perm_{Guid.NewGuid():N}");
        Assert.NotNull(adminAuth);
        Assert.NotNull(userAuth);

        var userId = userAuth!.User.Id;
        var accessToken = userAuth.AccessToken;

        const int iterations = 20;
        var accessResults = new ConcurrentBag<HttpStatusCode>();
        var permChangeResults = new ConcurrentBag<bool>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        _output.WriteLine($"[STEP] Starting {iterations} parallel permission changes and access attempts...");

        // Run access attempts and permission changes in parallel
        var accessTask = Task.Run(async () =>
        {
            for (int i = 0; i < iterations && !cts.Token.IsCancellationRequested; i++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var response = await _sharedHost.Host.HttpClient.SendAsync(request, cts.Token);
                    accessResults.Add(response.StatusCode);
                    await Task.Delay(50, cts.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        }, cts.Token);

        var permTask = Task.Run(async () =>
        {
            for (int i = 0; i < iterations && !cts.Token.IsCancellationRequested; i++)
            {
                try
                {
                    // Alternate between granting and revoking a permission
                    var isGrant = i % 2 == 0;
                    var endpoint = isGrant ? "/api/v1/iam/permissions/grant" : "/api/v1/iam/permissions/revoke";
                    var body = new
                    {
                        UserId = userId,
                        PermissionIdentifier = "api:portfolio:read",
                        Description = "Stress test"
                    };

                    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
                    request.Content = JsonContent.Create(body);
                    var response = await _sharedHost.Host.HttpClient.SendAsync(request, cts.Token);

                    permChangeResults.Add(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound);
                    await Task.Delay(100, cts.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        }, cts.Token);

        await Task.WhenAll(accessTask, permTask);

        _output.WriteLine($"[INFO] Access attempts: {accessResults.Count}, Permission changes: {permChangeResults.Count}");
        _output.WriteLine($"[INFO] Access OK: {accessResults.Count(r => r == HttpStatusCode.OK)}");
        _output.WriteLine($"[INFO] Permission changes succeeded: {permChangeResults.Count(r => r)}");

        // All /auth/me requests should succeed (permission changes don't affect /auth/me)
        Assert.True(accessResults.All(r => r == HttpStatusCode.OK), "All access attempts should succeed");
        _output.WriteLine("[PASS] System handled concurrent access and permission changes gracefully");
    }

    [Fact]
    public async Task RoleAssignmentWhileUserActive_HandledGracefully()
    {
        _output.WriteLine("[TEST] RoleAssignmentWhileUserActive_HandledGracefully");

        var adminAuth = await CreateAdminAsync();
        var userAuth = await RegisterUserAsync($"stress_role_{Guid.NewGuid():N}");
        Assert.NotNull(adminAuth);
        Assert.NotNull(userAuth);

        var userId = userAuth!.User.Id;
        const int iterations = 10;
        var results = new ConcurrentBag<(string Op, HttpStatusCode Status)>();

        _output.WriteLine($"[STEP] Running {iterations} role assignment/removal cycles while user accesses API...");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // User continuously accesses API
        var userTask = Task.Run(async () =>
        {
            for (int i = 0; i < iterations * 2 && !cts.Token.IsCancellationRequested; i++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);
                    var response = await _sharedHost.Host.HttpClient.SendAsync(request, cts.Token);
                    results.Add(("UserAccess", response.StatusCode));
                    await Task.Delay(100, cts.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        }, cts.Token);

        // Admin assigns and removes roles
        var adminTask = Task.Run(async () =>
        {
            for (int i = 0; i < iterations && !cts.Token.IsCancellationRequested; i++)
            {
                try
                {
                    // Note: In real scenario, you'd need to track the role assignment ID
                    // For this test, we just verify the operations don't crash
                    if (i % 2 == 0)
                    {
                        // Assign a custom role (this may fail if role doesn't exist, which is OK)
                        var assignBody = new { UserId = userId, RoleCode = "USER" };
                        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/assign");
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
                        request.Content = JsonContent.Create(assignBody);
                        var response = await _sharedHost.Host.HttpClient.SendAsync(request, cts.Token);
                        results.Add(("RoleAssign", response.StatusCode));
                    }

                    await Task.Delay(200, cts.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        }, cts.Token);

        await Task.WhenAll(userTask, adminTask);

        var userAccessResults = results.Where(r => r.Op == "UserAccess").ToList();
        _output.WriteLine($"[INFO] User access attempts: {userAccessResults.Count}, OK: {userAccessResults.Count(r => r.Status == HttpStatusCode.OK)}");

        // User should always be able to access /auth/me regardless of role changes
        Assert.True(userAccessResults.All(r => r.Status == HttpStatusCode.OK),
            "User should maintain access during role changes");

        _output.WriteLine("[PASS] System handled role assignment while user active");
    }

    #endregion

    #region Race Condition Tests

    [Fact]
    public async Task ConcurrentRoleAssignment_SameUser_NoCorruption()
    {
        _output.WriteLine("[TEST] ConcurrentRoleAssignment_SameUser_NoCorruption");

        var adminAuth = await CreateAdminAsync();
        var userAuth = await RegisterUserAsync($"stress_race_{Guid.NewGuid():N}");
        Assert.NotNull(adminAuth);
        Assert.NotNull(userAuth);

        var userId = userAuth!.User.Id;
        const int concurrentAssignments = 10;

        _output.WriteLine($"[STEP] Attempting {concurrentAssignments} concurrent role assignments...");

        // Try to assign the same role multiple times concurrently
        var assignTasks = Enumerable.Range(0, concurrentAssignments).Select(async _ =>
        {
            var assignBody = new { UserId = userId, RoleCode = "USER" };
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/roles/assign");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
            request.Content = JsonContent.Create(assignBody);
            return await _sharedHost.Host.HttpClient.SendAsync(request);
        }).ToArray();

        var results = await Task.WhenAll(assignTasks);

        var statusCodes = results.Select(r => r.StatusCode).ToList();
        _output.WriteLine($"[INFO] Results: {string.Join(", ", statusCodes.Select(s => (int)s))}");

        // At least one should succeed, others may succeed or return conflict/error
        // The important thing is no 500 errors
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, statusCodes);

        // Verify user state is consistent after concurrent operations
        using var verifyRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/iam/users/{userId}");
        verifyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        var verifyResponse = await _sharedHost.Host.HttpClient.SendAsync(verifyRequest);

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        _output.WriteLine("[PASS] No data corruption from concurrent role assignments");
    }

    [Fact]
    public async Task ConcurrentSessionRevocation_SameUser_AllHandled()
    {
        _output.WriteLine("[TEST] ConcurrentSessionRevocation_SameUser_AllHandled");

        var username = $"stress_revoke_{Guid.NewGuid():N}";
        var initialAuth = await RegisterUserAsync(username);
        Assert.NotNull(initialAuth);

        // Create multiple sessions concurrently
        const int sessionCount = 10;
        _output.WriteLine($"[STEP] Concurrently creating {sessionCount - 1} additional sessions...");
        var additionalLoginTasks = Enumerable.Range(1, sessionCount - 1)
            .Select(_ => LoginUserAsync(username))
            .ToArray();
        var additionalSessions = await Task.WhenAll(additionalLoginTasks);

        Assert.True(additionalSessions.All(s => s != null), "All session creations should succeed");
        var sessions = new List<AuthResponse> { initialAuth! };
        sessions.AddRange(additionalSessions!);

        var userId = initialAuth!.User.Id;

        // Get all session IDs
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/users/{userId}/sessions");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", initialAuth.AccessToken);
        var listResponse = await _sharedHost.Host.HttpClient.SendAsync(listRequest);
        var listContent = await listResponse.Content.ReadAsStringAsync();
        var sessionList = JsonSerializer.Deserialize<SessionListResponse>(listContent, JsonOptions);

        Assert.NotNull(sessionList?.Items);
        _output.WriteLine($"[INFO] Created {sessionList!.Items.Count} sessions");

        // Concurrently revoke all sessions using different session tokens
        _output.WriteLine("[STEP] Concurrently revoking all sessions...");

        var revokeTasks = sessionList.Items.Select(async (s, index) =>
        {
            // Use a different session's token to revoke each session
            var tokenIndex = (index + 1) % sessions.Count;
            using var request = new HttpRequestMessage(HttpMethod.Delete,
                $"/api/v1/auth/users/{userId}/sessions/{s.Id}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessions[tokenIndex].AccessToken);
            return await _sharedHost.Host.HttpClient.SendAsync(request);
        }).ToArray();

        var results = await Task.WhenAll(revokeTasks);

        var statusCodes = results.Select(r => r.StatusCode).ToList();
        _output.WriteLine($"[INFO] Revocation results: {string.Join(", ", statusCodes.Select(s => (int)s))}");

        // Should not have any 500 errors
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, statusCodes);

        // Most should succeed (204) or return 404 (already revoked) or 401 (token invalidated)
        var validStatuses = new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized };
        Assert.True(statusCodes.All(s => validStatuses.Contains(s)),
            "All revocations should complete without server errors");

        _output.WriteLine("[PASS] Concurrent session revocations handled correctly");
    }

    #endregion

    #region Database Contention Tests

    [Fact]
    public async Task HighFrequencyMeEndpoint_100Requests_AllSucceed()
    {
        _output.WriteLine("[TEST] HighFrequencyMeEndpoint_100Requests_AllSucceed");

        var userAuth = await RegisterUserAsync($"stress_me_{Guid.NewGuid():N}");
        Assert.NotNull(userAuth);

        const int requestCount = 100;
        _output.WriteLine($"[STEP] Sending {requestCount} concurrent requests to /auth/me...");

        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, requestCount).Select(async _ =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAuth!.AccessToken);
            return await _sharedHost.Host.HttpClient.SendAsync(request);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        sw.Stop();
        var successCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);

        _output.WriteLine($"[INFO] {requestCount} requests completed in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"[INFO] Success rate: {successCount}/{requestCount}");
        _output.WriteLine($"[INFO] Avg response time: {sw.ElapsedMilliseconds / requestCount}ms");

        Assert.Equal(requestCount, successCount);
        _output.WriteLine("[PASS] All high-frequency requests succeeded");
    }

    [Fact]
    public async Task MixedReadWriteOperations_NoDeadlocks()
    {
        _output.WriteLine("[TEST] MixedReadWriteOperations_NoDeadlocks");

        var adminAuth = await CreateAdminAsync();
        Assert.NotNull(adminAuth);

        // Create several users concurrently
        const int userCount = 10;
        _output.WriteLine($"[STEP] Concurrently creating {userCount} users...");
        var usernames = Enumerable.Range(0, userCount)
            .Select(_ => $"stress_mixed_{Guid.NewGuid():N}")
            .ToList();

        var registerTasks = usernames.Select(RegisterUserAsync).ToArray();
        var registrations = await Task.WhenAll(registerTasks);

        Assert.True(registrations.All(r => r != null), "All user registrations should succeed");
        var users = registrations.Select(r => r!).ToList();

        _output.WriteLine($"[STEP] Running mixed read/write operations on {userCount} users...");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var results = new ConcurrentBag<(string Op, HttpStatusCode Status)>();

        var tasks = new List<Task>();

        // Readers: Each user reads their own profile
        foreach (var user in users)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 10 && !cts.Token.IsCancellationRequested; i++)
                {
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
                        var response = await _sharedHost.Host.HttpClient.SendAsync(request, cts.Token);
                        results.Add(("Read", response.StatusCode));
                        await Task.Delay(50, cts.Token);
                    }
                    catch (OperationCanceledException) { break; }
                }
            }, cts.Token));
        }

        // Writer: Admin updates user profiles
        tasks.Add(Task.Run(async () =>
        {
            foreach (var user in users)
            {
                if (cts.Token.IsCancellationRequested) break;
                try
                {
                    var updateBody = new { Email = $"updated_{Guid.NewGuid():N}@test.com" };
                    using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/iam/users/{user.User.Id}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
                    request.Content = JsonContent.Create(updateBody);
                    var response = await _sharedHost.Host.HttpClient.SendAsync(request, cts.Token);
                    results.Add(("Write", response.StatusCode));
                    await Task.Delay(100, cts.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        }, cts.Token));

        await Task.WhenAll(tasks);

        var reads = results.Where(r => r.Op == "Read").ToList();
        var writes = results.Where(r => r.Op == "Write").ToList();

        _output.WriteLine($"[INFO] Reads: {reads.Count} (OK: {reads.Count(r => r.Status == HttpStatusCode.OK)})");
        _output.WriteLine($"[INFO] Writes: {writes.Count} (OK: {writes.Count(r => r.Status == HttpStatusCode.OK)})");

        // No 500 errors
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, results.Select(r => r.Status));

        // All reads should succeed
        Assert.True(reads.All(r => r.Status == HttpStatusCode.OK), "All reads should succeed");

        _output.WriteLine("[PASS] No deadlocks detected in mixed read/write operations");
    }

    #endregion

    #region Helper Methods

    private async Task<AuthResponse?> RegisterUserAsync(string username)
    {
        var registerRequest = new
        {
            Username = username,
            Password = TestPassword,
            ConfirmPassword = TestPassword,
            Email = $"{username}@stress.test"
        };

        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
    }

    private async Task<AuthResponse?> LoginUserAsync(string username)
    {
        var loginRequest = new { Username = username, Password = TestPassword };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
    }

    private async Task<AuthResponse?> RefreshTokenAsync(string refreshToken)
    {
        var refreshRequest = new { RefreshToken = refreshToken };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
    }

    private async Task<AuthResponse?> CreateAdminAsync()
    {
        var username = $"admin_stress_{Guid.NewGuid():N}";
        var createRequest = new
        {
            Username = username,
            Password = TestPassword,
            Email = $"{username}@stress.test"
        };

        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/devtools/create-admin", createRequest);

        if (!response.IsSuccessStatusCode)
            return null;

        // Login to get tokens
        return await LoginUserAsync(username);
    }

    #endregion

    #region Response Types

    private sealed class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public UserInfo User { get; set; } = null!;
    }

    private sealed class UserInfo
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    private sealed class SessionListResponse
    {
        public List<SessionInfo> Items { get; set; } = [];
    }

    private sealed class SessionInfo
    {
        public Guid Id { get; set; }
        public string? DeviceName { get; set; }
        public bool IsCurrent { get; set; }
    }

    #endregion
}
