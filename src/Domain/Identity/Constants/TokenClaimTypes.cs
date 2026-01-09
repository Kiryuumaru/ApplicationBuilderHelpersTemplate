namespace Domain.Identity.Constants;

/// <summary>
/// Standard claim type names used in JWT tokens.
/// Uses short claim names per RFC 7519 and RFC 9068.
/// </summary>
public static class TokenClaimTypes
{
    #region Identity Claims

    /// <summary>
    /// Subject (user ID) claim type. RFC 7519.
    /// </summary>
    public const string Subject = "sub";

    /// <summary>
    /// Name (username) claim type. RFC 7519.
    /// </summary>
    public const string Name = "name";

    /// <summary>
    /// Session ID claim type. RFC 7519.
    /// </summary>
    public const string SessionId = "sid";

    #endregion

    #region Authorization Claims

    /// <summary>
    /// Roles claim type. RFC 9068 Section 2.2.3.1 / RFC 7643 Section 4.1.2.
    /// </summary>
    public const string Roles = "roles";

    /// <summary>
    /// Scope claim type. RFC 9068.
    /// </summary>
    public const string Scope = "scope";

    #endregion

    #region Token Metadata Claims

    /// <summary>
    /// Token ID claim type. RFC 7519.
    /// </summary>
    public const string TokenId = "jti";

    /// <summary>
    /// Issued At claim type. RFC 7519.
    /// </summary>
    public const string IssuedAt = "iat";

    /// <summary>
    /// Expiration claim type. RFC 7519.
    /// </summary>
    public const string ExpiresAt = "exp";

    /// <summary>
    /// Token type header. RFC 9068.
    /// </summary>
    public const string TokenType = "typ";

    #endregion

    /// <summary>
    /// Token type values per RFC 9068.
    /// </summary>
    public static class TokenTypeValues
    {
        /// <summary>
        /// Access token type. RFC 9068.
        /// </summary>
        public const string AccessToken = "at+jwt";

        /// <summary>
        /// Refresh token type.
        /// </summary>
        public const string RefreshToken = "rt+jwt";

        /// <summary>
        /// API key token type.
        /// </summary>
        public const string ApiKey = "ak+jwt";
    }
}
