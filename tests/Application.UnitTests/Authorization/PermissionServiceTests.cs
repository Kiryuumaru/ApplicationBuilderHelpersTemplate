using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Authorization.Models;
using Application.Authorization.Services;
using Application.UnitTests.Authorization.Fakes;
using System.Linq;
using System.Security.Claims;
using JwtClaimTypes = Domain.Identity.Constants.JwtClaimTypes;

using AppTokenValidationResult = Application.Authorization.Models.TokenValidationResult;

namespace Application.UnitTests.Authorization;

public class PermissionServiceTests
{
    private const string AccountUpdatePermission = "api:portfolio:accounts:update;userId=user-123";
    private const string OrderCancelPermission = "api:trading:orders:cancel;userId=user-123";

    [Fact]
    public async Task GenerateTokenWithPermissionsAsync_NormalizesAndDelegatesScopes()
    {
        var tokenService = new RecordingTokenService();
        var service = CreateService(tokenService);

        var permissions = new[]
        {
            "  " + AccountUpdatePermission + "  ",
            AccountUpdatePermission,
            OrderCancelPermission
        };

        await service.GenerateTokenWithPermissionsAsync(
            userId: "user-123",
            username: "user@test",
            permissionIdentifiers: permissions,
            cancellationToken: CancellationToken.None);

        Assert.Equal([AccountUpdatePermission, OrderCancelPermission], tokenService.LastGeneratedScopes);
        Assert.Equal("user-123", tokenService.LastGenerateTokenUserId);
        Assert.Equal("user@test", tokenService.LastGenerateTokenUsername);
    }

    [Fact]
    public async Task HasPermission_RejectsLegacyToken()
    {
        var service = CreateService(new RecordingTokenService());
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await service.HasPermissionAsync(principal, OrderCancelPermission, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidatePermissionsAsync_AllowsAncestorScopedParameters()
    {
        var service = CreateService(new RecordingTokenService());

        var result = await service.ValidatePermissionsAsync(["api:_read;userId=user-123"]);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidatePermissionsAsync_RejectsUnknownParameters()
    {
        var service = CreateService(new RecordingTokenService());

        var result = await service.ValidatePermissionsAsync(["api:_read;tenantId=abc"]);

        Assert.False(result);
    }

    [Fact]
    public async Task HasPermission_AllowsUserScopedRootGrant()
    {
        var service = CreateService(new RecordingTokenService());
        // New directive format: allow;permission_path;param=value
        var identity = new ClaimsIdentity(
        [
            new Claim("scope", "allow;api:_read;userId=user-123"),
            new Claim("rbac_version", "2")
        ]);
        var principal = new ClaimsPrincipal(identity);

        // Requests carry parameters in semicolon notation
        var permitted = "api:portfolio:accounts:list;userId=user-123";
        var otherUser = "api:portfolio:accounts:list;userId=user-456";
        // Non-scoped request without userId - DENIED because scope requires userId
        var nonScoped = "api:favorites:read";

        Assert.True(await service.HasPermissionAsync(principal, permitted, CancellationToken.None));
        Assert.False(await service.HasPermissionAsync(principal, otherUser, CancellationToken.None));
        Assert.False(await service.HasPermissionAsync(principal, nonScoped, CancellationToken.None)); // Scope requires userId, request has none
    }

    [Fact]
    public async Task HasPermission_DeniesWhenParameterMissingOnRequest()
    {
        var service = CreateService(new RecordingTokenService());
        // Scope requires userId parameter
        var principal = BuildPrincipalWithScopes("allow;api:_read;userId=user-123");
        // Request has no userId parameter - the scope requires userId so this DENIES
        var permissionWithoutParameter = "api:favorites:read";

        // The scope says "allow api:_read only for userId=user-123"
        // A request without userId doesn't satisfy this - so it's DENIED
        Assert.False(await service.HasPermissionAsync(principal, permissionWithoutParameter, CancellationToken.None));
    }

    [Fact]
    public async Task HasPermission_DeniesAccessToOtherUser()
    {
        var service = CreateService(new RecordingTokenService());
        // Scope requires userId=user-123
        var principal = BuildPrincipalWithScopes("allow;api:_read;userId=user-123");

        // Attempt with userId=user-456 (different user)
        var attempt = "api:portfolio:accounts:list;userId=user-456";

        Assert.False(await service.HasPermissionAsync(principal, attempt, CancellationToken.None));
    }

    [Fact]
    public async Task HasAnyPermission_DeniesWhenOnlyOtherUserSpecified()
    {
        var service = CreateService(new RecordingTokenService());
        // Scope only allows user-123
        var principal = BuildPrincipalWithScopes("allow;api:_read;userId=user-123");

        // Both requests are for user-456 (different user)
        var otherUser = "api:portfolio:accounts:list;userId=user-456";
        var otherUserPositions = "api:trading:orders:read;userId=user-456";

        Assert.False(await service.HasAnyPermissionAsync(principal, [otherUser, otherUserPositions], CancellationToken.None));
    }

    [Fact]
    public async Task HasPermission_WriteScopeAllowsWriteOperations()
    {
        var service = CreateService(new RecordingTokenService());
        // Use new directive format with _write scope
        var principal = BuildPrincipalWithScopes("allow;api:_write;userId=user-123");

        var result = await service.HasPermissionAsync(principal, AccountUpdatePermission, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task HasPermission_ReadScopeDoesNotAllowWriteOperations()
    {
        var service = CreateService(new RecordingTokenService());
        // Only read scope, no write
        var principal = BuildPrincipalWithScopes("allow;api:_read;userId=user-123");

        var result = await service.HasPermissionAsync(principal, AccountUpdatePermission, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task HasAllPermissions_ReturnsFalseWhenAnyMissing()
    {
        var service = CreateService(new RecordingTokenService());
        // Only allows AccountUpdatePermission path, not position close
        var identity = new ClaimsIdentity(
        [
            new Claim("scope", "allow;api:portfolio:accounts:update;userId=user-123"),
            new Claim("rbac_version", "2")
        ]);
        var principal = new ClaimsPrincipal(identity);

        var result = await service.HasAllPermissionsAsync(principal, [AccountUpdatePermission, OrderCancelPermission], CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task MutateTokenAsync_NormalizesPermissionsBeforeDelegation()
    {
        var tokenService = new RecordingTokenService
        {
            ValidateTokenPrincipalResult = BuildPrincipalWithScopes(AccountUpdatePermission, OrderCancelPermission)
        };
        var service = CreateService(tokenService);

        var permissionsToAdd = new[] { "  " + OrderCancelPermission + " ", OrderCancelPermission };
        var permissionsToRemove = new[] { AccountUpdatePermission, "  " + AccountUpdatePermission };

        var result = await service.MutateTokenAsync(
            token: "token-value",
            permissionsToAdd: permissionsToAdd,
            permissionsToRemove: permissionsToRemove,
            claimsToAdd: [new Claim("tenant", "alpha")],
            claimsToRemove: [new Claim("tenant", "beta")],
            claimTypesToRemove: ["custom_type"],
            cancellationToken: CancellationToken.None);

        Assert.Equal("mutated-token", result);
        Assert.Null(tokenService.LastMutateScopesToAdd);
        Assert.Equal([AccountUpdatePermission], tokenService.LastMutateScopesToRemove);
        Assert.Equal("token-value", tokenService.LastMutatedToken);
    }

    [Fact]
    public async Task ValidateTokenAsync_PassesThroughUnderlyingService()
    {
        var principal = BuildPrincipalWithScopes(AccountUpdatePermission);
        var tokenService = new RecordingTokenService { ValidateTokenPrincipalResult = principal };
        var service = CreateService(tokenService);

        var result = await service.ValidateTokenAsync("token", CancellationToken.None);

        Assert.Same(principal, result);
    }

    private static ClaimsPrincipal BuildPrincipalWithScopes(params string[] scopes)
    {
        var scopeValue = string.Join(' ', scopes);
        var identity = new ClaimsIdentity(
        [
            new Claim("scope", scopeValue),
            new Claim("rbac_version", "2")
        ]);

        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal BuildLegacyPrincipalWithScopes(params string[] scopes)
    {
        var scopeValue = string.Join(' ', scopes);
        var identity = new ClaimsIdentity(
        [
            new Claim("scope", scopeValue),
            new Claim("rbac_version", "1")
        ]);

        return new ClaimsPrincipal(identity);
    }

    private static PermissionService CreateService(RecordingTokenService tokenService)
        => new(tokenService, new InMemoryRoleRepository());

    private sealed class RecordingTokenService : ITokenService
    {
        public string GenerateTokenResult { get; set; } = "token";
        public string GenerateApiKeyTokenResult { get; set; } = "api-token";
        public string MutateTokenResult { get; set; } = "mutated-token";
        public ClaimsPrincipal? ValidateTokenPrincipalResult { get; set; }
        public TokenInfo? DecodeTokenResult { get; set; }

        public string? LastGenerateTokenUserId { get; private set; }
        public string? LastGenerateTokenUsername { get; private set; }
        public IReadOnlyCollection<string>? LastGeneratedScopes { get; private set; }
        public string? LastApiKeyName { get; private set; }
        public IReadOnlyCollection<string>? LastApiKeyScopes { get; private set; }
        public string? LastMutatedToken { get; private set; }
        public IReadOnlyCollection<string>? LastMutateScopesToAdd { get; private set; }
        public IReadOnlyCollection<string>? LastMutateScopesToRemove { get; private set; }

        public Task<string> GenerateAccessTokenAsync(Guid userId, string? username, IReadOnlyCollection<string> roleCodes, IEnumerable<Claim>? additionalClaims = null, CancellationToken cancellationToken = default)
        {
            LastGenerateTokenUserId = userId.ToString();
            LastGenerateTokenUsername = username;
            LastGeneratedScopes = roleCodes.ToArray();
            return Task.FromResult(GenerateTokenResult);
        }

        public Task<string> GenerateRefreshTokenAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult("refresh-token");

        public Task<AppTokenValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (ValidateTokenPrincipalResult is null)
            {
                return Task.FromResult(AppTokenValidationResult.Failed("Token validation failed."));
            }

            var userIdClaim = ValidateTokenPrincipalResult.FindFirst(JwtClaimTypes.Subject)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Task.FromResult(AppTokenValidationResult.Failed("Invalid user ID."));
            }

            var roles = ValidateTokenPrincipalResult.FindAll(JwtClaimTypes.Roles).Select(c => c.Value).ToArray();
            return Task.FromResult(AppTokenValidationResult.Success(userId, null, roles));
        }

        public Task<string> GenerateTokenWithScopesAsync(string userId, string? username, IEnumerable<string> scopes, IEnumerable<Claim>? additionalClaims = null, DateTimeOffset? expiration = null, Domain.Identity.Enums.TokenType tokenType = Domain.Identity.Enums.TokenType.Access, CancellationToken cancellationToken = default)
        {
            LastGenerateTokenUserId = userId;
            LastGenerateTokenUsername = username;
            LastGeneratedScopes = scopes?.ToArray();
            return Task.FromResult(GenerateTokenResult);
        }

        public Task<string> GenerateApiKeyTokenAsync(string apiKeyName, IEnumerable<string>? scopes = null, IEnumerable<Claim>? additionalClaims = null, DateTimeOffset? expiration = null, CancellationToken cancellationToken = default)
        {
            LastApiKeyName = apiKeyName;
            LastApiKeyScopes = scopes?.ToArray();
            return Task.FromResult(GenerateApiKeyTokenResult);
        }

        public Task<ClaimsPrincipal?> ValidateTokenPrincipalAsync(string token, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ValidateTokenPrincipalResult);
        }

        public Task<TokenInfo?> DecodeTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(DecodeTokenResult);

        public Task<string> MutateTokenAsync(string token, IEnumerable<string>? scopesToAdd = null, IEnumerable<string>? scopesToRemove = null, IEnumerable<Claim>? claimsToAdd = null, IEnumerable<Claim>? claimsToRemove = null, IEnumerable<string>? claimTypesToRemove = null, DateTimeOffset? expiration = null, CancellationToken cancellationToken = default)
        {
            LastMutatedToken = token;
            LastMutateScopesToAdd = scopesToAdd?.ToArray();
            LastMutateScopesToRemove = scopesToRemove?.ToArray();
            return Task.FromResult(MutateTokenResult);
        }
    }
}
