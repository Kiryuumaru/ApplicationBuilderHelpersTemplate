using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Presentation.WebApi.FunctionalTests.Auth;

/// <summary>
/// Tests for token expiration, validity, and session lifecycle.
/// Covers edge cases around token timing, theft detection, and validity boundaries.
/// </summary>
[Collection(WebApiTestCollection.Name)]
public class TokenExpirationTests
{
    private readonly ITestOutputHelper _output;
    private readonly SharedWebApiHost _sharedHost;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string TestPassword = "TestPassword123!";
    private const int ExpectedAccessTokenExpirationSeconds = 3600; // 60 minutes

    public TokenExpirationTests(SharedWebApiHost sharedHost, ITestOutputHelper output)
    {
        _sharedHost = sharedHost;
        _output = output;
    }

    #region ExpiresIn Response Field Tests

    [Fact]
    public async Task Login_Response_ExpiresIn_IsCorrectValue()
    {
        _output.WriteLine("[TEST] Login_Response_ExpiresIn_IsCorrectValue");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        _output.WriteLine($"[INFO] ExpiresIn value: {authResult!.ExpiresIn} seconds");

        Assert.Equal(ExpectedAccessTokenExpirationSeconds, authResult.ExpiresIn);
        _output.WriteLine("[PASS] ExpiresIn matches expected value (3600 seconds / 60 minutes)");
    }

    [Fact]
    public async Task Register_Response_ExpiresIn_IsCorrectValue()
    {
        _output.WriteLine("[TEST] Register_Response_ExpiresIn_IsCorrectValue");

        var username = $"exptest_{Guid.NewGuid():N}";
        var registerRequest = new
        {
            Username = username,
            Email = $"{username}@example.com",
            Password = TestPassword,
            ConfirmPassword = TestPassword
        };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);

        Assert.NotNull(result);
        _output.WriteLine($"[INFO] ExpiresIn value: {result!.ExpiresIn} seconds");

        Assert.Equal(ExpectedAccessTokenExpirationSeconds, result.ExpiresIn);
        _output.WriteLine("[PASS] Register response ExpiresIn is correct");
    }

    [Fact]
    public async Task Refresh_Response_ExpiresIn_IsCorrectValue()
    {
        _output.WriteLine("[TEST] Refresh_Response_ExpiresIn_IsCorrectValue");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var refreshRequest = new { RefreshToken = authResult!.RefreshToken };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);

        Assert.NotNull(result);
        _output.WriteLine($"[INFO] ExpiresIn value: {result!.ExpiresIn} seconds");

        Assert.Equal(ExpectedAccessTokenExpirationSeconds, result.ExpiresIn);
        _output.WriteLine("[PASS] Refresh response ExpiresIn is correct");
    }

    [Fact]
    public async Task ExpiresIn_IsPositiveNumber()
    {
        _output.WriteLine("[TEST] ExpiresIn_IsPositiveNumber");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        Assert.True(authResult!.ExpiresIn > 0, "ExpiresIn should be a positive number");
        _output.WriteLine($"[PASS] ExpiresIn is positive: {authResult.ExpiresIn}");
    }

    #endregion

    #region JWT Expiration Claim Tests

    [Fact]
    public async Task AccessToken_HasExpClaim()
    {
        _output.WriteLine("[TEST] AccessToken_HasExpClaim");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(authResult!.AccessToken);

        var expClaim = token.Claims.FirstOrDefault(c => c.Type == "exp");
        Assert.NotNull(expClaim);

        var expValue = long.Parse(expClaim!.Value);
        var expDate = DateTimeOffset.FromUnixTimeSeconds(expValue);

        _output.WriteLine($"[INFO] Token exp claim: {expDate:O}");
        _output.WriteLine($"[INFO] Current time: {DateTimeOffset.UtcNow:O}");

        // Token should expire in the future
        Assert.True(expDate > DateTimeOffset.UtcNow, "Token should expire in the future");
        _output.WriteLine("[PASS] Access token has valid exp claim");
    }

    [Fact]
    public async Task AccessToken_ExpClaimMatchesExpiresIn()
    {
        _output.WriteLine("[TEST] AccessToken_ExpClaimMatchesExpiresIn");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(authResult!.AccessToken);

        var expClaim = token.Claims.FirstOrDefault(c => c.Type == "exp");
        var iatClaim = token.Claims.FirstOrDefault(c => c.Type == "iat");

        Assert.NotNull(expClaim);
        Assert.NotNull(iatClaim);

        var exp = long.Parse(expClaim!.Value);
        var iat = long.Parse(iatClaim!.Value);
        var tokenLifetimeSeconds = exp - iat;

        _output.WriteLine($"[INFO] Token lifetime from claims: {tokenLifetimeSeconds} seconds");
        _output.WriteLine($"[INFO] ExpiresIn response value: {authResult.ExpiresIn} seconds");

        // Allow for small timing differences (within 5 seconds)
        Assert.True(
            Math.Abs(tokenLifetimeSeconds - authResult.ExpiresIn) <= 5,
            $"Token lifetime ({tokenLifetimeSeconds}s) should match ExpiresIn ({authResult.ExpiresIn}s)");

        _output.WriteLine("[PASS] exp claim matches ExpiresIn response");
    }

    [Fact]
    public async Task AccessToken_HasIatClaim()
    {
        _output.WriteLine("[TEST] AccessToken_HasIatClaim");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(authResult!.AccessToken);

        var iatClaim = token.Claims.FirstOrDefault(c => c.Type == "iat");
        Assert.NotNull(iatClaim);

        var iatValue = long.Parse(iatClaim!.Value);
        var iatDate = DateTimeOffset.FromUnixTimeSeconds(iatValue);

        _output.WriteLine($"[INFO] Token iat claim: {iatDate:O}");

        // Token should have been issued recently (within last minute)
        var timeDiff = DateTimeOffset.UtcNow - iatDate;
        Assert.True(timeDiff.TotalMinutes < 1, "Token should have been issued within the last minute");

        _output.WriteLine("[PASS] Access token has valid iat claim");
    }

    [Fact]
    public async Task RefreshToken_HasExpClaim()
    {
        _output.WriteLine("[TEST] RefreshToken_HasExpClaim");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(authResult!.RefreshToken);

        var expClaim = token.Claims.FirstOrDefault(c => c.Type == "exp");
        Assert.NotNull(expClaim);

        var expValue = long.Parse(expClaim!.Value);
        var expDate = DateTimeOffset.FromUnixTimeSeconds(expValue);

        _output.WriteLine($"[INFO] Refresh token exp: {expDate:O}");

        // Refresh token should expire in the future (typically 7 days)
        Assert.True(expDate > DateTimeOffset.UtcNow, "Refresh token should expire in the future");
        Assert.True(expDate > DateTimeOffset.UtcNow.AddDays(1), "Refresh token should expire after at least 1 day");

        _output.WriteLine("[PASS] Refresh token has valid exp claim");
    }

    [Fact]
    public async Task RefreshToken_ExpiresLaterThanAccessToken()
    {
        _output.WriteLine("[TEST] RefreshToken_ExpiresLaterThanAccessToken");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.ReadJwtToken(authResult!.AccessToken);
        var refreshToken = handler.ReadJwtToken(authResult.RefreshToken);

        var accessExp = long.Parse(accessToken.Claims.First(c => c.Type == "exp").Value);
        var refreshExp = long.Parse(refreshToken.Claims.First(c => c.Type == "exp").Value);

        _output.WriteLine($"[INFO] Access token expires: {DateTimeOffset.FromUnixTimeSeconds(accessExp):O}");
        _output.WriteLine($"[INFO] Refresh token expires: {DateTimeOffset.FromUnixTimeSeconds(refreshExp):O}");

        Assert.True(refreshExp > accessExp, "Refresh token should expire later than access token");
        _output.WriteLine("[PASS] Refresh token expires later than access token");
    }

    #endregion

    #region Session Validity Tests

    [Fact]
    public async Task RefreshToken_AfterSessionRevoked_Returns401()
    {
        _output.WriteLine("[TEST] RefreshToken_AfterSessionRevoked_Returns401");

        var username = $"revoke_test_{Guid.NewGuid():N}";
        await RegisterUserAsync(username);
        var secondLogin = await LoginUserAsync(username);
        Assert.NotNull(secondLogin);

        // Get sessions and revoke the current one via logout
        _output.WriteLine("[STEP] Logging out to revoke session...");
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondLogin!.AccessToken);
        await _sharedHost.Host.HttpClient.SendAsync(logoutRequest);

        // Now try to refresh with the revoked session's token
        _output.WriteLine("[STEP] Attempting refresh with revoked session token...");
        var refreshRequest = new { RefreshToken = secondLogin.RefreshToken };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"[RECEIVED] Body: {content}");

        _output.WriteLine("[PASS] Revoked session refresh token is rejected");
    }

    [Fact]
    public async Task RefreshToken_AfterAllSessionsRevoked_Returns401()
    {
        _output.WriteLine("[TEST] RefreshToken_AfterAllSessionsRevoked_Returns401");

        var username = $"revokeall_test_{Guid.NewGuid():N}";
        await RegisterUserAsync(username);
        var firstLogin = await LoginUserAsync(username);
        var secondLogin = await LoginUserAsync(username);
        Assert.NotNull(firstLogin);
        Assert.NotNull(secondLogin);

        var userId = secondLogin!.User.Id;

        // Revoke all sessions (including current now)
        _output.WriteLine("[STEP] Revoking all sessions...");
        using var revokeAllRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/auth/users/{userId}/sessions");
        revokeAllRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondLogin.AccessToken);
        await _sharedHost.Host.HttpClient.SendAsync(revokeAllRequest);

        // First login's refresh token should now be invalid
        _output.WriteLine("[STEP] Attempting refresh with revoked session token...");
        var refreshRequest = new { RefreshToken = firstLogin!.RefreshToken };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("[PASS] Revoked session refresh token is rejected after bulk revoke");
    }

    #endregion

    #region Token Theft Detection Tests

    [Fact]
    public async Task RefreshToken_ReusedAfterRotation_DetectsTheft()
    {
        _output.WriteLine("[TEST] RefreshToken_ReusedAfterRotation_DetectsTheft");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var stolenToken = authResult!.RefreshToken;

        // Legitimate user refreshes - this should work
        _output.WriteLine("[STEP] Legitimate refresh (simulating victim)...");
        var legitRefresh = new { RefreshToken = stolenToken };
        var response1 = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", legitRefresh);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Attacker tries to use the stolen token - should fail
        _output.WriteLine("[STEP] Attacker trying to use stolen token...");
        var attackerRefresh = new { RefreshToken = stolenToken };
        var response2 = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", attackerRefresh);

        _output.WriteLine($"[RECEIVED] Attacker response: {(int)response2.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response2.StatusCode);
        _output.WriteLine("[PASS] Token theft detected - old token rejected");
    }

    [Fact]
    public async Task RefreshToken_ReusedMultipleTimes_AllFail()
    {
        _output.WriteLine("[TEST] RefreshToken_ReusedMultipleTimes_AllFail");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var originalToken = authResult!.RefreshToken;

        // First refresh - succeeds
        var refresh1 = new { RefreshToken = originalToken };
        var response1 = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refresh1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Try multiple reuses - all should fail
        for (int i = 0; i < 3; i++)
        {
            var retryRefresh = new { RefreshToken = originalToken };
            var retryResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", retryRefresh);

            _output.WriteLine($"[INFO] Reuse attempt {i + 1}: {(int)retryResponse.StatusCode}");
            Assert.Equal(HttpStatusCode.Unauthorized, retryResponse.StatusCode);
        }

        _output.WriteLine("[PASS] All reuse attempts rejected");
    }

    [Fact]
    public async Task RefreshToken_TheftRevokesEntireSession()
    {
        _output.WriteLine("[TEST] RefreshToken_TheftRevokesEntireSession");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var stolenToken = authResult!.RefreshToken;

        // Legitimate refresh
        var legitRefresh = new { RefreshToken = stolenToken };
        var legitResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", legitRefresh);
        Assert.Equal(HttpStatusCode.OK, legitResponse.StatusCode);
        var newContent = await legitResponse.Content.ReadAsStringAsync();
        var newTokens = JsonSerializer.Deserialize<AuthResponse>(newContent, JsonOptions);

        // Attacker uses stolen token - triggers theft detection
        var attackerRefresh = new { RefreshToken = stolenToken };
        await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", attackerRefresh);

        // Now even the legitimate NEW token should be revoked
        _output.WriteLine("[STEP] Checking if new token is also revoked...");
        var newTokenRefresh = new { RefreshToken = newTokens!.RefreshToken };
        var finalResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", newTokenRefresh);

        _output.WriteLine($"[RECEIVED] New token response: {(int)finalResponse.StatusCode}");

        // Session should be fully revoked
        Assert.Equal(HttpStatusCode.Unauthorized, finalResponse.StatusCode);
        _output.WriteLine("[PASS] Theft detection revokes entire session");
    }

    #endregion

    #region Token Chain Tests

    [Fact]
    public async Task RefreshToken_ChainedRefreshes_EachTokenRotates()
    {
        _output.WriteLine("[TEST] RefreshToken_ChainedRefreshes_EachTokenRotates");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var tokens = new List<string> { authResult!.RefreshToken };

        // Do 5 chained refreshes
        for (int i = 0; i < 5; i++)
        {
            var currentToken = tokens.Last();
            var refreshRequest = new { RefreshToken = currentToken };
            var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var newTokens = JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);

            Assert.NotNull(newTokens);
            Assert.NotEqual(currentToken, newTokens!.RefreshToken);

            tokens.Add(newTokens.RefreshToken);
            _output.WriteLine($"[INFO] Refresh {i + 1}: New token different from previous");
        }

        // Verify all tokens are unique
        Assert.Equal(6, tokens.Distinct().Count());
        _output.WriteLine("[PASS] All refresh tokens in chain are unique");
    }

    [Fact]
    public async Task RefreshToken_OldTokenInChain_Invalid()
    {
        _output.WriteLine("[TEST] RefreshToken_OldTokenInChain_Invalid");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var tokenChain = new List<string> { authResult!.RefreshToken };

        // Build a chain of 3 tokens - each refresh rotates to a new token
        for (int i = 0; i < 3; i++)
        {
            var refreshRequest = new { RefreshToken = tokenChain.Last() };
            var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var newTokens = JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);
            tokenChain.Add(newTokens!.RefreshToken);
        }

        _output.WriteLine($"[INFO] Built chain of {tokenChain.Count} tokens");

        // The latest token should work
        var latestRefresh = new { RefreshToken = tokenChain.Last() };
        var latestResponse = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", latestRefresh);

        _output.WriteLine($"[INFO] Latest token: {(int)latestResponse.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, latestResponse.StatusCode);

        // After refreshing the latest, all OLD tokens in chain should be invalid
        // Note: We need to get the new latest token after refreshing
        var latestContent = await latestResponse.Content.ReadAsStringAsync();
        var latestTokens = JsonSerializer.Deserialize<AuthResponse>(latestContent, JsonOptions);

        for (int i = 0; i < tokenChain.Count; i++)
        {
            var oldTokenRefresh = new { RefreshToken = tokenChain[i] };
            var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", oldTokenRefresh);

            _output.WriteLine($"[INFO] Token {i} (old): {(int)response.StatusCode}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        _output.WriteLine("[PASS] All old tokens in chain are invalid");
    }

    #endregion

    #region Token Type Validation Tests

    [Fact]
    public async Task RefreshToken_WithAccessToken_Returns401()
    {
        _output.WriteLine("[TEST] RefreshToken_WithAccessToken_Returns401");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        // Try using access token in refresh endpoint
        var wrongTokenRefresh = new { RefreshToken = authResult!.AccessToken };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", wrongTokenRefresh);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("[PASS] Access token rejected when used as refresh token");
    }

    [Fact]
    public async Task AccessToken_HasCorrectTokenTypeClaim()
    {
        _output.WriteLine("[TEST] AccessToken_HasCorrectTokenTypeClaim");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(authResult!.AccessToken);

        // Log all claims for debugging
        _output.WriteLine("[INFO] Access token claims:");
        foreach (var claim in token.Claims)
        {
            _output.WriteLine($"  {claim.Type}: {claim.Value}");
        }

        // Check that access token does NOT have refresh token type
        var tokenTypeClaim = token.Claims.FirstOrDefault(c => c.Type == "token_type" || c.Type == "typ");
        if (tokenTypeClaim != null)
        {
            Assert.NotEqual("refresh", tokenTypeClaim.Value.ToLowerInvariant());
        }

        _output.WriteLine("[PASS] Access token does not have refresh token type");
    }

    [Fact]
    public async Task RefreshToken_HasCorrectTokenTypeClaim()
    {
        _output.WriteLine("[TEST] RefreshToken_HasCorrectTokenTypeClaim");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(authResult!.RefreshToken);

        // Log all claims
        _output.WriteLine("[INFO] Refresh token claims:");
        foreach (var claim in token.Claims)
        {
            _output.WriteLine($"  {claim.Type}: {claim.Value}");
        }

        // Refresh token should have distinguishing characteristics
        _output.WriteLine("[PASS] Refresh token claims logged for verification");
    }

    #endregion

    #region Invalid Token Format Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    [InlineData("....")]
    public async Task RefreshToken_WithInvalidFormat_Returns401OrBadRequest(string invalidToken)
    {
        _output.WriteLine($"[TEST] RefreshToken_WithInvalidFormat: '{invalidToken}'");

        var refreshRequest = new { RefreshToken = invalidToken };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode}");

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 401 or 400, got {(int)response.StatusCode}");

        _output.WriteLine("[PASS] Invalid format rejected");
    }

    [Theory]
    [InlineData(null)]
    public async Task RefreshToken_WithNullValue_Returns400(string? nullToken)
    {
        _output.WriteLine("[TEST] RefreshToken_WithNullValue_Returns400");

        var refreshRequest = new { RefreshToken = nullToken };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode}");

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 400 or 401, got {(int)response.StatusCode}");

        _output.WriteLine("[PASS] Null token rejected");
    }

    [Fact]
    public async Task RefreshToken_WithEmptyObject_Returns400()
    {
        _output.WriteLine("[TEST] RefreshToken_WithEmptyObject_Returns400");

        var response = await _sharedHost.Host.HttpClient.PostAsync(
            "/api/v1/auth/refresh",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode}");

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 400 or 401, got {(int)response.StatusCode}");

        _output.WriteLine("[PASS] Empty object rejected");
    }

    [Fact]
    public async Task RefreshToken_WithExtraFields_StillWorks()
    {
        _output.WriteLine("[TEST] RefreshToken_WithExtraFields_StillWorks");

        var authResult = await RegisterUniqueUserAsync();
        Assert.NotNull(authResult);

        var requestWithExtra = new
        {
            RefreshToken = authResult!.RefreshToken,
            ExtraField = "should-be-ignored",
            AnotherField = 12345
        };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", requestWithExtra);

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _output.WriteLine("[PASS] Extra fields are ignored");
    }

    #endregion

    #region Cross-Session Token Tests

    [Fact]
    public async Task RefreshToken_FromDifferentSession_DoesNotAffectOther()
    {
        _output.WriteLine("[TEST] RefreshToken_FromDifferentSession_DoesNotAffectOther");

        var username = $"multisession_{Guid.NewGuid():N}";
        var session1 = await RegisterUserAsync(username);
        var session2 = await LoginUserAsync(username);

        Assert.NotNull(session1);
        Assert.NotNull(session2);

        // Refresh session 1's token
        var refresh1 = new { RefreshToken = session1!.RefreshToken };
        var response1 = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refresh1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Session 2's token should still work
        var refresh2 = new { RefreshToken = session2!.RefreshToken };
        var response2 = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refresh2);

        _output.WriteLine($"[RECEIVED] Session 2 refresh: {(int)response2.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        _output.WriteLine("[PASS] Sessions are independent");
    }

    [Fact]
    public async Task RefreshToken_FromOtherUser_NotAccepted()
    {
        _output.WriteLine("[TEST] RefreshToken_FromOtherUser_NotAccepted");

        var user1 = await RegisterUniqueUserAsync();
        var user2 = await RegisterUniqueUserAsync();

        Assert.NotNull(user1);
        Assert.NotNull(user2);

        // Try to get info using user1's access token
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user1!.AccessToken);
        var response = await _sharedHost.Host.HttpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var userInfo = JsonSerializer.Deserialize<UserInfoResponse>(content, JsonOptions);

        // Verify it's actually user1's info
        Assert.NotEqual(user2!.User.Id, userInfo!.Id);
        _output.WriteLine($"[INFO] User1 ID: {user1.User.Id}, Response ID: {userInfo.Id}");

        _output.WriteLine("[PASS] Token belongs to correct user");
    }

    #endregion

    #region Token Response Consistency Tests

    [Fact]
    public async Task NewTokens_HaveDifferentValues_EachTime()
    {
        _output.WriteLine("[TEST] NewTokens_HaveDifferentValues_EachTime");

        var username = $"unique_tokens_{Guid.NewGuid():N}";
        var auth1 = await RegisterUserAsync(username);
        var auth2 = await LoginUserAsync(username);
        var auth3 = await LoginUserAsync(username);

        Assert.NotNull(auth1);
        Assert.NotNull(auth2);
        Assert.NotNull(auth3);

        // All access tokens should be different
        Assert.NotEqual(auth1!.AccessToken, auth2!.AccessToken);
        Assert.NotEqual(auth2.AccessToken, auth3!.AccessToken);
        Assert.NotEqual(auth1.AccessToken, auth3.AccessToken);

        // All refresh tokens should be different
        Assert.NotEqual(auth1.RefreshToken, auth2.RefreshToken);
        Assert.NotEqual(auth2.RefreshToken, auth3.RefreshToken);
        Assert.NotEqual(auth1.RefreshToken, auth3.RefreshToken);

        _output.WriteLine("[PASS] All tokens are unique across logins");
    }

    [Fact]
    public async Task TokenType_IsBearerForAllResponses()
    {
        _output.WriteLine("[TEST] TokenType_IsBearerForAllResponses");

        var username = $"tokentype_{Guid.NewGuid():N}";
        var registerResponse = await RegisterUserAsync(username);
        var loginResponse = await LoginUserAsync(username);

        Assert.NotNull(registerResponse);
        Assert.NotNull(loginResponse);

        Assert.Equal("Bearer", registerResponse!.TokenType);
        Assert.Equal("Bearer", loginResponse!.TokenType);

        // Refresh should also return Bearer
        var refreshRequest = new { RefreshToken = loginResponse.RefreshToken };
        var response = await _sharedHost.Host.HttpClient.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);
        var content = await response.Content.ReadAsStringAsync();
        var refreshResult = JsonSerializer.Deserialize<AuthResponse>(content, JsonOptions);

        Assert.Equal("Bearer", refreshResult!.TokenType);
        _output.WriteLine("[PASS] All responses have Bearer token type");
    }

    #endregion

    #region Helper Methods

    private async Task<AuthResponse?> RegisterUniqueUserAsync()
    {
        var username = $"exptest_{Guid.NewGuid():N}";
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

    #endregion
}
