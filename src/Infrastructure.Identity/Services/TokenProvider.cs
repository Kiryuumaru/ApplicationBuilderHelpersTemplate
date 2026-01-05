using Application.Authorization.Interfaces.Infrastructure;
using Application.Authorization.Models;
using Infrastructure.Identity.Interfaces;
using System.Security.Claims;

namespace Infrastructure.Identity.Services;

internal class TokenProvider(IJwtTokenService jwtTokenService) : ITokenProvider
{
    public async Task<string> GenerateAccessTokenAsync(
        Guid userId,
        string? username,
        IReadOnlyCollection<string> roleCodes,
        IEnumerable<Claim>? additionalClaims = null,
        CancellationToken cancellationToken = default)
    {
        var claims = new List<Claim>();
        
        foreach (var role in roleCodes)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        
        if (additionalClaims is not null)
        {
            claims.AddRange(additionalClaims);
        }
        
        return await jwtTokenService.GenerateToken(
            userId: userId.ToString(),
            username: username ?? userId.ToString(),
            scopes: null,
            additionalClaims: claims,
            expiration: null,
            cancellationToken: cancellationToken);
    }

    public Task<string> GenerateRefreshTokenAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        // Generate a simple refresh token using session ID
        // In a real implementation, this would have its own signing mechanism
        return Task.FromResult(Convert.ToBase64String(sessionId.ToByteArray()) + "." + Guid.NewGuid().ToString("N"));
    }

    public async Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var principal = await jwtTokenService.ValidateToken(token, cancellationToken);
        
        if (principal is null)
        {
            return TokenValidationResult.Failed("Token validation failed");
        }
        
        var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = userIdClaim is not null && Guid.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : Guid.Empty;
        
        var sessionIdClaim = principal.FindFirstValue("session_id");
        var sessionId = sessionIdClaim is not null && Guid.TryParse(sessionIdClaim, out var parsedSessionId) ? parsedSessionId : (Guid?)null;
        
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        
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
        return await jwtTokenService.GenerateToken(
            userId: userId,
            username: username ?? userId,
            scopes: scopes,
            additionalClaims: additionalClaims,
            expiration: expiration,
            cancellationToken: cancellationToken);
    }

    public async Task<string> GenerateApiKeyTokenAsync(
        string apiKeyName,
        IEnumerable<string>? scopes = null,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        return await jwtTokenService.GenerateApiKeyToken(
            apiKeyName: apiKeyName,
            scopes: scopes,
            additionalClaims: additionalClaims,
            expiration: expiration,
            cancellationToken: cancellationToken);
    }

    public async Task<ClaimsPrincipal?> ValidateTokenPrincipalAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return await jwtTokenService.ValidateToken(token, cancellationToken);
    }

    public async Task<TokenInfo?> DecodeTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return await jwtTokenService.DecodeToken(token, cancellationToken);
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
        return await jwtTokenService.MutateToken(
            token: token,
            scopesToAdd: scopesToAdd,
            scopesToRemove: scopesToRemove,
            claimsToAdd: claimsToAdd,
            claimsToRemove: claimsToRemove,
            claimTypesToRemove: claimTypesToRemove,
            expiration: expiration,
            cancellationToken: cancellationToken);
    }
}
