using Application.Authorization.Models;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Application.Authorization.Interfaces;

/// <summary>
/// Interface for JWT token generation and validation services.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT token for the specified user with optional claims and expiration.
    /// </summary>
    /// <param name="userId">The unique identifier for the user.</param>
    /// <param name="username">The username or email of the user.</param>
    /// <param name="additionalClaims">Additional claims to include in the token.</param>
    /// <param name="expiration">Custom expiration time. If null, uses default expiration.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A JWT token string.</returns>
    Task<string> GenerateToken(
        string userId,
        string username,
        IEnumerable<string>? scopes = null,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a simple API key token with minimal claims.
    /// Useful for service-to-service authentication.
    /// </summary>
    /// <param name="apiKeyName">The name or identifier for the API key.</param>
    /// <param name="scopes">Optional scopes or permissions for this API key.</param>
    /// <param name="expiration">Custom expiration time. If null, uses default expiration.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A JWT token string suitable for API key authentication.</returns>
    Task<string> GenerateApiKeyToken(
        string apiKeyName, 
        IEnumerable<string>? scopes = null,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a JWT token and returns the claims principal if valid.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The claims principal if the token is valid, null otherwise.</returns>
    Task<ClaimsPrincipal?> ValidateToken(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the <see cref="TokenValidationParameters"/> to validates a JWT token.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The <see cref="TokenValidationParameters"/> to validates a JWT tokene.</returns>
    Task<TokenValidationParameters> GetTokenValidationParameters(CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts token information without full validation.
    /// Useful for debugging or logging purposes.
    /// </summary>
    /// <param name="token">The JWT token to decode.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>Token information including claims and expiration.</returns>
    Task<TokenInfo?> DecodeToken(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reissues an existing token after applying claim mutations.
    /// </summary>
    /// <param name="token">The source token to mutate.</param>
    /// <param name="claimsToAdd">Claim instances to add to the reissued token.</param>
    /// <param name="claimsToRemove">Specific claims to remove from the token (matched by type and value).</param>
    /// <param name="claimTypesToRemove">Claim types to remove entirely from the token.</param>
    /// <param name="expiration">Optional expiration override for the reissued token.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The reissued token string.</returns>
    Task<string> MutateToken(
        string token,
        IEnumerable<string>? scopesToAdd = null,
        IEnumerable<string>? scopesToRemove = null,
        IEnumerable<Claim>? claimsToAdd = null,
        IEnumerable<Claim>? claimsToRemove = null,
        IEnumerable<string>? claimTypesToRemove = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default);
}
