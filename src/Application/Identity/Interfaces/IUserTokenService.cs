using Application.Identity.Models;

namespace Application.Identity.Interfaces;

/// <summary>
/// Service for user authentication token generation and rotation.
/// Coordinates token generation with session management atomically.
/// </summary>
public interface IUserTokenService
{
    /// <summary>
    /// Creates a new login session and generates access/refresh tokens atomically.
    /// Fetches user information (username) internally from the userId.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="deviceInfo">Optional device information from HTTP context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Token pair and session ID.</returns>
    Task<UserTokenResult> CreateSessionWithTokensAsync(
        Guid userId,
        SessionDeviceInfo? deviceInfo,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rotates tokens for an existing session atomically.
    /// Generates new access/refresh tokens and updates the session with the new refresh token hash.
    /// </summary>
    /// <param name="sessionId">The existing session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>New token pair and session ID.</returns>
    Task<UserTokenResult> RotateTokensAsync(
        Guid sessionId,
        CancellationToken cancellationToken);
}
