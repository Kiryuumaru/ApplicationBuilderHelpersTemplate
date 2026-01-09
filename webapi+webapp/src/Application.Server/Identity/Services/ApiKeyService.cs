using System.Security.Claims;
using Application.Server.Authorization.Interfaces;
using Application.Server.Identity.Interfaces;
using Application.Server.Identity.Interfaces.Infrastructure;
using Application.Server.Identity.Models;
using Domain.Authorization.Constants;
using Domain.Authorization.ValueObjects;
using Domain.Identity.Constants;
using Domain.Identity.Models;
using TokenClaimTypes = Domain.Identity.Constants.TokenClaimTypes;
using TokenType = Domain.Identity.Enums.TokenType;

namespace Application.Server.Identity.Services;

/// <summary>
/// Service for managing user API keys.
/// </summary>
public sealed class ApiKeyService(
    IApiKeyRepository apiKeyRepository,
    IUserAuthorizationService userAuthorizationService,
    IPermissionService permissionService) : IApiKeyService
{
    /// <summary>
    /// Maximum number of API keys allowed per user.
    /// </summary>
    private const int MaxApiKeysPerUser = 100;

    /// <inheritdoc />
    public async Task<(ApiKeyDto Metadata, string Token)> CreateAsync(
        Guid userId,
        string name,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        // Enforce max keys limit
        var currentCount = await apiKeyRepository.GetActiveCountByUserIdAsync(userId, cancellationToken);
        if (currentCount >= MaxApiKeysPerUser)
        {
            throw new InvalidOperationException($"Maximum number of API keys ({MaxApiKeysPerUser}) reached.");
        }

        // Generate API key ID
        var keyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Create API key entity
        var apiKey = new ApiKey
        {
            Id = keyId,
            UserId = userId,
            Name = name,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        // Generate the token
        var token = await GenerateApiKeyTokenAsync(userId, keyId, expiresAt, cancellationToken);

        // Store metadata in DB
        await apiKeyRepository.CreateAsync(apiKey, cancellationToken);

        // Return DTO and token
        var dto = MapToDto(apiKey);
        return (dto, token);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiKeyDto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var apiKeys = await apiKeyRepository.GetByUserIdAsync(userId, cancellationToken);
        return apiKeys.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ApiKeyDto?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var apiKey = await apiKeyRepository.GetByIdAsync(id, cancellationToken);
        if (apiKey is null || apiKey.UserId != userId || apiKey.IsRevoked)
        {
            return null;
        }
        return MapToDto(apiKey);
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var apiKey = await apiKeyRepository.GetByIdAsync(id, cancellationToken);
        if (apiKey is null || apiKey.UserId != userId || apiKey.IsRevoked)
        {
            return false;
        }

        apiKey.IsRevoked = true;
        apiKey.RevokedAt = DateTimeOffset.UtcNow;
        await apiKeyRepository.UpdateAsync(apiKey, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<ApiKey?> ValidateApiKeyAsync(Guid keyId, CancellationToken cancellationToken)
    {
        var apiKey = await apiKeyRepository.GetByIdAsync(keyId, cancellationToken);
        if (apiKey is null)
        {
            return null;
        }

        // Check if revoked
        if (apiKey.IsRevoked)
        {
            return null;
        }

        // Check if expired (DB-side check, JWT exp is also validated by middleware)
        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return apiKey;
    }

    /// <inheritdoc />
    public async Task UpdateLastUsedAsync(Guid keyId, CancellationToken cancellationToken)
    {
        var apiKey = await apiKeyRepository.GetByIdAsync(keyId, cancellationToken);
        if (apiKey is not null && !apiKey.IsRevoked)
        {
            apiKey.LastUsedAt = DateTimeOffset.UtcNow;
            await apiKeyRepository.UpdateAsync(apiKey, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<int> GetActiveCountAsync(Guid userId, CancellationToken cancellationToken)
    {
        return apiKeyRepository.GetActiveCountByUserIdAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Generates an API key token.
    /// Same as access token but with:
    /// - typ: <see cref="TokenType.ApiKey"/>
    /// - jti = keyId for revocation lookup
    /// - Explicit deny for api:auth:refresh and api:auth:api_keys
    /// </summary>
    private async Task<string> GenerateApiKeyTokenAsync(
        Guid userId,
        Guid keyId,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        // Get user authorization data
        var authData = await userAuthorizationService.GetAuthorizationDataAsync(userId, cancellationToken);

        // Add roles (same as access token)
        var additionalClaims = new List<Claim>();
        foreach (var role in authData.FormattedRoles)
        {
            additionalClaims.Add(new Claim(TokenClaimTypes.Roles, role));
        }

        // Get direct permission scopes (same as access token)
        var scopes = authData.DirectPermissionScopes.Select(ScopeDirective.Parse).ToList();

        // API keys CANNOT refresh tokens
        scopes.Add(ScopeDirective.Parse(PermissionIds.Api.Auth.Refresh.WithUserId(userId.ToString()).Deny()));

        // API keys CANNOT manage API keys - use _read/_write wildcards
        // _read covers: list
        // _write covers: create, revoke
        var userIdParam = $";userId={userId}";
        scopes.Add(ScopeDirective.Parse($"deny;api:auth:api_keys:_read{userIdParam}"));
        scopes.Add(ScopeDirective.Parse($"deny;api:auth:api_keys:_write{userIdParam}"));

        // Use very far future expiration if none specified
        var tokenExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddYears(100);

        return await permissionService.GenerateTokenWithScopeAsync(
            userId.ToString(),
            authData.Username ?? string.Empty,
            scopes,
            additionalClaims,
            tokenExpiresAt,
            TokenType.ApiKey,
            keyId.ToString(),
            cancellationToken);
    }

    private static ApiKeyDto MapToDto(ApiKey apiKey) => new()
    {
        Id = apiKey.Id,
        UserId = apiKey.UserId,
        Name = apiKey.Name,
        CreatedAt = apiKey.CreatedAt,
        ExpiresAt = apiKey.ExpiresAt,
        LastUsedAt = apiKey.LastUsedAt
    };
}
