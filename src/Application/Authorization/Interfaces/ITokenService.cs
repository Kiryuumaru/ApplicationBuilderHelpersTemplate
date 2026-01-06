using Application.Authorization.Models;
using System.Security.Claims;

namespace Application.Authorization.Interfaces;

/// <summary>
/// Consumer-level service for token generation and validation operations.
/// Implemented by Application layer.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates an access token for a user with the specified roles and scope.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="username">The username (null for anonymous users).</param>
    /// <param name="roleCodes">The role codes to include in the token.</param>
    /// <param name="additionalClaims">Additional claims to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated access token.</returns>
    Task<string> GenerateAccessTokenAsync(
        Guid userId,
        string? username,
        IReadOnlyCollection<string> roleCodes,
        IEnumerable<Claim>? additionalClaims = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a refresh token for a session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated refresh token.</returns>
    Task<string> GenerateRefreshTokenAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an access token and extracts claims.
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a token with the specified permissions/scopes.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="username">The username (null for anonymous users).</param>
    /// <param name="scopes">The permission scopes to include.</param>
    /// <param name="additionalClaims">Additional claims to include.</param>
    /// <param name="expiration">Optional expiration override.</param>
    /// <param name="tokenType">The type of token to generate (Access, Refresh, or ApiKey).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated token.</returns>
    Task<string> GenerateTokenWithScopesAsync(
        string userId,
        string? username,
        IEnumerable<string> scopes,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        Domain.Identity.Enums.TokenType tokenType = Domain.Identity.Enums.TokenType.Access,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an API key token with the specified permissions/scopes.
    /// </summary>
    /// <param name="apiKeyName">The API key name.</param>
    /// <param name="scopes">The permission scopes to include.</param>
    /// <param name="additionalClaims">Additional claims to include.</param>
    /// <param name="expiration">Optional expiration override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated API key token.</returns>
    Task<string> GenerateApiKeyTokenAsync(
        string apiKeyName,
        IEnumerable<string>? scopes = null,
        IEnumerable<Claim>? additionalClaims = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a token and returns the claims principal.
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The claims principal if valid, null otherwise.</returns>
    Task<ClaimsPrincipal?> ValidateTokenPrincipalAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decodes a token without validation.
    /// </summary>
    /// <param name="token">The token to decode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The token info if decodable, null otherwise.</returns>
    Task<TokenInfo?> DecodeTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mutates an existing token by adding/removing scopes and claims.
    /// </summary>
    /// <param name="token">The token to mutate.</param>
    /// <param name="scopesToAdd">Scopes to add.</param>
    /// <param name="scopesToRemove">Scopes to remove.</param>
    /// <param name="claimsToAdd">Claims to add.</param>
    /// <param name="claimsToRemove">Claims to remove.</param>
    /// <param name="claimTypesToRemove">Claim types to remove entirely.</param>
    /// <param name="expiration">Optional new expiration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mutated token.</returns>
    Task<string> MutateTokenAsync(
        string token,
        IEnumerable<string>? scopesToAdd = null,
        IEnumerable<string>? scopesToRemove = null,
        IEnumerable<Claim>? claimsToAdd = null,
        IEnumerable<Claim>? claimsToRemove = null,
        IEnumerable<string>? claimTypesToRemove = null,
        DateTimeOffset? expiration = null,
        CancellationToken cancellationToken = default);
}
