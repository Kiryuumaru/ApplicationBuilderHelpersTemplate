namespace Domain.Identity.Enums;

/// <summary>
/// Identifies the type of token being generated or validated.
/// Per RFC 9068, the typ header distinguishes token purposes.
/// </summary>
public enum TokenType
{
    /// <summary>
    /// Access token for API authorization.
    /// </summary>
    Access,

    /// <summary>
    /// Refresh token for obtaining new access tokens.
    /// </summary>
    Refresh,

    /// <summary>
    /// API key token for machine-to-machine access.
    /// </summary>
    ApiKey
}
