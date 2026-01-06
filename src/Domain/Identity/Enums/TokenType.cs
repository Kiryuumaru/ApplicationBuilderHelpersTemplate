namespace Domain.Identity.Enums;

/// <summary>
/// Identifies the type of JWT token being generated or validated.
/// Per RFC 9068, the typ header distinguishes token purposes.
/// </summary>
public enum TokenType
{
    /// <summary>
    /// Access token for API authorization. typ: "at+jwt" per RFC 9068.
    /// </summary>
    Access,

    /// <summary>
    /// Refresh token for obtaining new access tokens. typ: "rt+jwt".
    /// </summary>
    Refresh,

    /// <summary>
    /// API key token for machine-to-machine access. typ: "ak+jwt".
    /// </summary>
    ApiKey
}
