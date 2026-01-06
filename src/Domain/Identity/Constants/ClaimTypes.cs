namespace Domain.Identity.Constants;

/// <summary>
/// Standard claim type names used in JWT tokens.
/// Uses short claim names (not verbose Microsoft schema URIs).
/// </summary>
public static class ClaimTypes
{
    /// <summary>
    /// Subject (user ID) claim type.
    /// </summary>
    public const string Subject = "sub";

    /// <summary>
    /// Name (username) claim type.
    /// </summary>
    public const string Name = "name";

    /// <summary>
    /// Session ID claim type.
    /// </summary>
    public const string SessionId = "sid";

    /// <summary>
    /// Role claim type.
    /// </summary>
    public const string Role = "role";
}
