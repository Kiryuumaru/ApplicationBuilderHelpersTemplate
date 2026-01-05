using Application.Authorization.Interfaces.Infrastructure;
using Application.Authorization.Models;
using Infrastructure.EFCore.Identity.Interfaces;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Infrastructure.EFCore.Identity.Services;

/// <summary>
/// Implementation of ITokenProvider that wraps the internal IJwtTokenService.
/// </summary>
internal sealed class TokenProvider(
    Func<CancellationToken, Task<IJwtTokenService>> jwtTokenServiceFactory) : ITokenProvider
{
    private readonly Func<CancellationToken, Task<IJwtTokenService>> _jwtTokenServiceFactory = jwtTokenServiceFactory ?? throw new ArgumentNullException(nameof(jwtTokenServiceFactory));

    public async Task<string> GenerateAccessTokenAsync(
        Guid userId,
        string? username,
        IReadOnlyCollection<string> roleCodes,
        IEnumerable<Claim>? additionalClaims = null,
        CancellationToken cancellationToken = default)
    {
        var jwtService = await _jwtTokenServiceFactory(cancellationToken).ConfigureAwait(false);

        return await jwtService.GenerateToken(
            userId: userId.ToString(),
            username: username ?? string.Empty,
            scopes: roleCodes,
            additionalClaims: additionalClaims,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public Task<string> GenerateRefreshTokenAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken; // Reserved for future async operations

        // Generate 256-bit cryptographically secure random token combined with session ID
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        var randomPart = Convert.ToBase64String(bytes);
        return Task.FromResult($"{sessionId}:{randomPart}");
    }

    public async Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return TokenValidationResult.Failed("Token is required.");
        }

        var jwtService = await _jwtTokenServiceFactory(cancellationToken).ConfigureAwait(false);
        var principal = await jwtService.ValidateToken(token, cancellationToken).ConfigureAwait(false);

        if (principal is null)
        {
            return TokenValidationResult.Failed("Token validation failed.");
        }

        // Extract claims
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return TokenValidationResult.Failed("Invalid user ID in token.");
        }

        var sessionIdClaim = principal.FindFirst("session_id")?.Value;
        Guid? sessionId = Guid.TryParse(sessionIdClaim, out var sid) ? sid : null;

        var roles = principal.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray();

        return TokenValidationResult.Success(userId, sessionId, roles);
    }

    public async Task<string> GenerateTokenWithScopesAsync(
        string userId,
        string? username,
        IEnumerable<string> scopes,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var jwtService = await _jwtTokenServiceFactory(cancellationToken).ConfigureAwait(false);

        return await jwtService.GenerateToken(
            userId: userId,
            username: username ?? string.Empty,
            scopes: scopes,
            additionalClaims: additionalClaims,
            expiration: expiration,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GenerateApiKeyTokenAsync(
        string apiKeyName,
        IEnumerable<string>? scopes = null,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var jwtService = await _jwtTokenServiceFactory(cancellationToken).ConfigureAwait(false);

        return await jwtService.GenerateApiKeyToken(
            apiKeyName: apiKeyName,
            scopes: scopes,
            additionalClaims: additionalClaims,
            expiration: expiration,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClaimsPrincipal?> ValidateTokenPrincipalAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var jwtService = await _jwtTokenServiceFactory(cancellationToken).ConfigureAwait(false);
        return await jwtService.ValidateToken(token, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TokenInfo?> DecodeTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var jwtService = await _jwtTokenServiceFactory(cancellationToken).ConfigureAwait(false);
        return await jwtService.DecodeToken(token, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> MutateTokenAsync(
        string token,
        IEnumerable<string>? scopesToAdd = null,
        IEnumerable<string>? scopesToRemove = null,
        IEnumerable<Claim>? claimsToAdd = null,
        IEnumerable<Claim>? claimsToRemove = null,
        IEnumerable<string>? claimTypesToRemove = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var jwtService = await _jwtTokenServiceFactory(cancellationToken).ConfigureAwait(false);

        return await jwtService.MutateToken(
            token: token,
            scopesToAdd: scopesToAdd,
            scopesToRemove: scopesToRemove,
            claimsToAdd: claimsToAdd,
            claimsToRemove: claimsToRemove,
            claimTypesToRemove: claimTypesToRemove,
            expiration: expiration,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
