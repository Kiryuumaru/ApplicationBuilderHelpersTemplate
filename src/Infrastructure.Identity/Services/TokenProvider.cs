using Application.Authorization.Interfaces.Infrastructure;
using Application.Authorization.Models;
using Domain.Identity.Enums;
using Infrastructure.Identity.Interfaces;
using System.Security.Claims;
using JwtClaimTypes = Domain.Identity.Constants.JwtClaimTypes;

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
        
        // RFC 9068 Section 2.2.3.1 / RFC 7643 Section 4.1.2 specify "roles" (plural)
        foreach (var role in roleCodes)
        {
            claims.Add(new Claim(JwtClaimTypes.Roles, role));
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
            tokenType: TokenType.Access,
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
        var principal = await jwtTokenService.ValidateToken(token, expectedType: null, cancellationToken);
        
        if (principal is null)
        {
            return TokenValidationResult.Failed("Token validation failed");
        }
        
        var userIdClaim = principal.FindFirstValue(JwtClaimTypes.Subject);
        var userId = userIdClaim is not null && Guid.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : Guid.Empty;
        
        var sessionIdClaim = principal.FindFirstValue(JwtClaimTypes.SessionId);
        var sessionId = sessionIdClaim is not null && Guid.TryParse(sessionIdClaim, out var parsedSessionId) ? parsedSessionId : (Guid?)null;
        
        // RFC 9068 Section 2.2.3.1 specifies "roles" (plural)
        var roles = principal.FindAll(JwtClaimTypes.Roles).Select(c => c.Value).Distinct().ToList();
        
        return TokenValidationResult.Success(userId, sessionId, roles);
    }

    public async Task<string> GenerateTokenWithScopesAsync(
        string userId,
        string? username,
        IEnumerable<string> scopes,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        TokenType tokenType = TokenType.Access,
        CancellationToken cancellationToken = default)
    {
        return await jwtTokenService.GenerateToken(
            userId: userId,
            username: username ?? userId,
            scopes: scopes,
            additionalClaims: additionalClaims,
            expiration: expiration,
            tokenType: tokenType,
            cancellationToken: cancellationToken);
    }

    public async Task<string> GenerateApiKeyTokenAsync(
        string apiKeyName,
        IEnumerable<string>? scopes = null,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var claims = new List<Claim>
        {
            new("api_key", "true")
        };

        if (additionalClaims is not null)
        {
            claims.AddRange(additionalClaims);
        }

        return await jwtTokenService.GenerateToken(
            userId: apiKeyName,
            username: apiKeyName,
            scopes: scopes,
            additionalClaims: claims,
            expiration: expiration,
            tokenType: TokenType.ApiKey,
            cancellationToken: cancellationToken);
    }

    public async Task<ClaimsPrincipal?> ValidateTokenPrincipalAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        return await jwtTokenService.ValidateToken(token, expectedType: null, cancellationToken);
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
