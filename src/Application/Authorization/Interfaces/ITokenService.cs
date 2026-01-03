using Application.Authorization.Models;
using System.Security.Claims;

namespace Application.Authorization.Interfaces;

/// <summary>
/// Service for token generation and validation operations.
/// Abstracts JWT implementation details from the Application layer.
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
}
