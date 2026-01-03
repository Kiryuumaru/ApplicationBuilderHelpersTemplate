using Application.Identity.Models;
using Domain.Identity.Models;

namespace Application.Identity.Interfaces;

public interface IIdentityService
{
    /// <summary>
    /// Registers a new user.
    /// If request is null, creates an anonymous (guest) user with no credentials.
    /// Anonymous users can later link an identity to upgrade to a full account.
    /// </summary>
    Task<User> RegisterUserAsync(UserRegistrationRequest? request, CancellationToken cancellationToken);

    Task<User> RegisterExternalAsync(ExternalUserRegistrationRequest request, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<User>> ListAsync(CancellationToken cancellationToken);

    Task<UserSession> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a session for an externally authenticated user (e.g., OAuth).
    /// No password verification is performed - caller must have already verified external identity.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A user session.</returns>
    Task<UserSession> CreateSessionForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task AssignRoleAsync(Guid userId, RoleAssignmentRequest assignment, CancellationToken cancellationToken);

    Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken);

    Task UpdateUserAsync(Guid userId, UserUpdateRequest request, CancellationToken cancellationToken);

    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken);

    Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken);

    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken);

    // Two-Factor Authentication
    Task<TwoFactorSetupInfo> Setup2faAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> Enable2faAsync(Guid userId, string verificationCode, CancellationToken cancellationToken);

    Task Disable2faAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> Verify2faCodeAsync(Guid userId, string code, CancellationToken cancellationToken);

    Task<AuthenticationResult> AuthenticateWithResultAsync(string username, string password, CancellationToken cancellationToken);

    Task<UserSession> Complete2faAuthenticationAsync(Guid userId, string code, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GenerateRecoveryCodesAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> GetRecoveryCodeCountAsync(Guid userId, CancellationToken cancellationToken);

    // Identity Linking
    /// <summary>
    /// Links a password to a user's account, upgrading anonymous users to full accounts.
    /// </summary>
    Task LinkPasswordAsync(Guid userId, string username, string password, string? email, CancellationToken cancellationToken);

    /// <summary>
    /// Links an email to a user's account.
    /// Email alone does not upgrade anonymous users - they need a password, OAuth, or passkey.
    /// </summary>
    Task LinkEmailAsync(Guid userId, string email, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the user's username.
    /// </summary>
    Task ChangeUsernameAsync(Guid userId, string newUsername, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the user's email address.
    /// </summary>
    Task ChangeEmailAsync(Guid userId, string newEmail, CancellationToken cancellationToken);

    /// <summary>
    /// Unlinks the email from the user's account.
    /// </summary>
    Task UnlinkEmailAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Upgrades an anonymous user to a full account after linking a passkey.
    /// Passkeys are passwordless so no username is required.
    /// </summary>
    Task UpgradeAnonymousWithPasskeyAsync(Guid userId, CancellationToken cancellationToken);

    // Password Reset
    Task<string?> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken);

    Task<bool> ResetPasswordWithTokenAsync(string email, string token, string newPassword, CancellationToken cancellationToken);
}
