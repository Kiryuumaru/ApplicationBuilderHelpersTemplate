using Application.Authorization.Models;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Infrastructure.Identity.Interfaces;

/// <summary>
/// Internal interface for JWT token operations.
/// This is an infrastructure interface, implemented by services that handle JWT details.
/// </summary>
internal interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT token for the specified user.
    /// </summary>
    Task<string> GenerateToken(
        string userId,
        string username,
        IEnumerable<string>? scopes = null,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an API key token.
    /// </summary>
    Task<string> GenerateApiKeyToken(
        string apiKeyName,
        IEnumerable<string>? scopes = null,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a JWT token and returns the claims principal.
    /// </summary>
    Task<ClaimsPrincipal?> ValidateToken(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decodes a JWT token without validation.
    /// </summary>
    Task<TokenInfo?> DecodeToken(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mutates an existing token by adding/removing scopes and claims.
    /// </summary>
    Task<string> MutateToken(
        string token,
        IEnumerable<string>? scopesToAdd = null,
        IEnumerable<string>? scopesToRemove = null,
        IEnumerable<Claim>? claimsToAdd = null,
        IEnumerable<Claim>? claimsToRemove = null,
        IEnumerable<string>? claimTypesToRemove = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the token validation parameters for validating JWTs.
    /// </summary>
    Task<TokenValidationParameters> GetTokenValidationParameters(CancellationToken cancellationToken = default);
}
