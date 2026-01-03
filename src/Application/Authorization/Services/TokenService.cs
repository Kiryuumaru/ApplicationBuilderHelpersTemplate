using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Authorization.Models;
using System.Security.Claims;

namespace Application.Authorization.Services;

/// <summary>
/// Implementation of ITokenService that wraps the internal IJwtTokenService.
/// </summary>
internal sealed class TokenService(
    Func<CancellationToken, Task<IJwtTokenService>> jwtTokenServiceFactory) : ITokenService
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

    public async Task<string> GenerateRefreshTokenAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        // Generate a secure refresh token
        // For now, we just encode the session ID - in production, use a more secure approach
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        var token = Convert.ToBase64String(bytes);
        return await Task.FromResult($"{sessionId}:{token}").ConfigureAwait(false);
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
}
