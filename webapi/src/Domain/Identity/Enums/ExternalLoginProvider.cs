namespace Domain.Identity.Enums;

/// <summary>
/// Supported external OAuth login providers.
/// </summary>
public enum ExternalLoginProvider
{
    /// <summary>
    /// Google OAuth 2.0 provider.
    /// </summary>
    Google,

    /// <summary>
    /// GitHub OAuth provider.
    /// </summary>
    GitHub,

    /// <summary>
    /// Microsoft Account OAuth 2.0 provider.
    /// </summary>
    Microsoft,

    /// <summary>
    /// Discord OAuth 2.0 provider.
    /// </summary>
    Discord,

    /// <summary>
    /// Mock/Test provider for development and testing.
    /// </summary>
    Mock
}
