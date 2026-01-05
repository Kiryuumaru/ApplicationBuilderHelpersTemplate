using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Authorization.Models;
using System.Security.Claims;

namespace Application.Authorization.Services;

/// <summary>
/// Application layer implementation of ITokenService.
/// Delegates to ITokenProvider from Infrastructure layer.
/// </summary>
internal sealed class TokenService(
    ITokenProvider tokenProvider) : ITokenService
{
    private readonly ITokenProvider _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));

    public Task<string> GenerateAccessTokenAsync(
        Guid userId,
        string? username,
        IReadOnlyCollection<string> roleCodes,
        IEnumerable<Claim>? additionalClaims = null,
        CancellationToken cancellationToken = default)
    {
        return _tokenProvider.GenerateAccessTokenAsync(userId, username, roleCodes, additionalClaims, cancellationToken);
    }

    public Task<string> GenerateRefreshTokenAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return _tokenProvider.GenerateRefreshTokenAsync(sessionId, cancellationToken);
    }

    public Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return _tokenProvider.ValidateTokenAsync(token, cancellationToken);
    }

    public Task<string> GenerateTokenWithScopesAsync(
        string userId,
        string? username,
        IEnumerable<string> scopes,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        return _tokenProvider.GenerateTokenWithScopesAsync(userId, username, scopes, additionalClaims, expiration, cancellationToken);
    }

    public Task<string> GenerateApiKeyTokenAsync(
        string apiKeyName,
        IEnumerable<string>? scopes = null,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        return _tokenProvider.GenerateApiKeyTokenAsync(apiKeyName, scopes, additionalClaims, expiration, cancellationToken);
    }

    public Task<ClaimsPrincipal?> ValidateTokenPrincipalAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return _tokenProvider.ValidateTokenPrincipalAsync(token, cancellationToken);
    }

    public Task<TokenInfo?> DecodeTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return _tokenProvider.DecodeTokenAsync(token, cancellationToken);
    }

    public Task<string> MutateTokenAsync(
        string token,
        IEnumerable<string>? scopesToAdd = null,
        IEnumerable<string>? scopesToRemove = null,
        IEnumerable<Claim>? claimsToAdd = null,
        IEnumerable<Claim>? claimsToRemove = null,
        IEnumerable<string>? claimTypesToRemove = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        return _tokenProvider.MutateTokenAsync(token, scopesToAdd, scopesToRemove, claimsToAdd, claimsToRemove, claimTypesToRemove, expiration, cancellationToken);
    }
}
