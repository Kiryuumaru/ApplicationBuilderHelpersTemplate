using Domain.Identity.Models;

namespace Application.Identity.Models;

/// <summary>
/// Result of authentication that may require 2FA.
/// </summary>
public sealed record AuthenticationResult
{
    /// <summary>
    /// Whether authentication was successful.
    /// </summary>
    public bool Succeeded { get; init; }
    
    /// <summary>
    /// Whether 2FA is required to complete authentication.
    /// </summary>
    public bool RequiresTwoFactor { get; init; }
    
    /// <summary>
    /// The user session if authentication succeeded without 2FA.
    /// </summary>
    public UserSession? Session { get; init; }
    
    /// <summary>
    /// The user ID for 2FA completion (only set when RequiresTwoFactor is true).
    /// </summary>
    public Guid? TwoFactorUserId { get; init; }

    public static AuthenticationResult Success(UserSession session) => new()
    {
        Succeeded = true,
        RequiresTwoFactor = false,
        Session = session
    };

    public static AuthenticationResult TwoFactorRequired(Guid userId) => new()
    {
        Succeeded = false,
        RequiresTwoFactor = true,
        TwoFactorUserId = userId
    };

    public static AuthenticationResult Failed() => new()
    {
        Succeeded = false,
        RequiresTwoFactor = false
    };
}
