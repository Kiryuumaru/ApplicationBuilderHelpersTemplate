using Application.Client.Authentication.Models;

namespace Application.Client.Authentication.Interfaces;

/// <summary>
/// Interface for user profile operations.
/// </summary>
public interface IUserProfileClient
{
    /// <summary>
    /// Gets the current user's profile from the /me endpoint.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's profile information.</returns>
    Task<UserProfile?> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the current user's password.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="currentPassword">The current password.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
