namespace Domain.Identity.Constants;

/// <summary>
/// Token and session expiration constants.
/// </summary>
public static class TokenExpirations
{
    /// <summary>
    /// Default access token expiration (1 hour).
    /// </summary>
    public static readonly TimeSpan AccessToken = TimeSpan.FromHours(1);

    /// <summary>
    /// Default refresh token expiration (7 days).
    /// </summary>
    public static readonly TimeSpan RefreshToken = TimeSpan.FromDays(7);

    /// <summary>
    /// Default passkey challenge expiration (5 minutes).
    /// </summary>
    public static readonly TimeSpan PasskeyChallenge = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default passkey session expiration (24 hours).
    /// </summary>
    public static readonly TimeSpan PasskeySession = TimeSpan.FromHours(24);
}
