using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Server.Identity.Models;

/// <summary>
/// Configuration settings for JSON Web Token (JWT) authentication.
/// </summary>
/// <remarks>
/// <para>
/// This class provides comprehensive configuration for JWT token generation and validation
/// in the authentication system. It supports standard JWT claims and 
/// security parameters for token lifecycle management.
/// </para>
/// <para>
/// JWT tokens are used for stateless authentication across the distributed system,
/// providing secure communication between edge clients and cloud services.
/// </para>
/// <para>
/// Security Considerations:
/// - The Secret should be cryptographically strong (minimum 256 bits for HMAC-SHA256)
/// - Issuer and Audience claims should be environment-specific
/// - Token expiration should balance security with user experience
/// - Clock skew tolerance helps with distributed system time synchronization
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var jwtConfig = new JwtConfiguration
/// {
///     Secret = "your-256-bit-secret-key-here",
///     Issuer = "https://auth.viana.ai",
///     Audience = "https://api.viana.ai",
///     DefaultExpiration = TimeSpan.FromHours(24),
///     DefaultClockSkew = TimeSpan.FromMinutes(5)
/// };
/// </code>
/// </example>
public class JwtConfiguration
{
    /// <summary>
    /// Gets the secret key used for signing and validating JWT tokens.
    /// </summary>
    /// <value>
    /// A cryptographically strong secret used for HMAC-SHA256 signing. 
    /// Should be at least 256 bits (32 bytes) for security compliance.
    /// Can be provided as a base64-encoded string or plain text.
    /// </value>
    /// <remarks>
    /// <para>
    /// This secret is critical for JWT security. It should be:
    /// - Generated using a cryptographically secure random number generator
    /// - Stored securely (environment variables, key vault, etc.)
    /// - Rotated regularly according to security policies
    /// - Never exposed in logs or source code
    /// </para>
    /// <para>
    /// In production environments, consider using asymmetric keys (RS256) 
    /// instead of symmetric keys for enhanced security.
    /// </para>
    /// </remarks>
    /// <example>
    /// Good: "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890"
    /// </example>
    public required string Secret { get; init; }

    /// <summary>
    /// Gets the issuer claim (iss) that identifies the principal that issued the JWT.
    /// </summary>
    /// <value>
    /// The issuer identifier, typically a URL or application name that uniquely 
    /// identifies the token issuing authority. Must match during token validation.
    /// </value>
    /// <remarks>
    /// <para>
    /// The issuer claim is a registered JWT claim defined in RFC 7519. It identifies
    /// the principal that issued the JWT and is used to validate token authenticity.
    /// </para>
    /// <para>
    /// Best practices:
    /// - Use URLs for global uniqueness (e.g., "https://auth.example.com")
    /// - Include environment in the issuer for multi-environment setups
    /// - Keep consistent across the application ecosystem
    /// - Validate strictly during token verification
    /// </para>
    /// </remarks>
    public required string Issuer { get; init; }

    /// <summary>
    /// Gets the audience claim (aud) that identifies the recipients that the JWT is intended for.
    /// </summary>
    /// <value>
    /// The audience identifier, typically a URL or application identifier that specifies
    /// the intended recipients of the token. Must match during token validation.
    /// </value>
    /// <remarks>
    /// <para>
    /// The audience claim is a registered JWT claim defined in RFC 7519. It identifies
    /// the recipients that the JWT is intended for, providing an additional layer
    /// of security by ensuring tokens are used only by intended services.
    /// </para>
    /// <para>
    /// Best practices:
    /// - Use specific audience values for different APIs or services
    /// - Include environment-specific identifiers
    /// - Validate audience claims strictly during verification
    /// - Consider using multiple audiences for tokens shared across services
    /// </para>
    /// </remarks>
    public required string Audience { get; init; }

    /// <summary>
    /// Gets or sets the default expiration time for JWT tokens.
    /// </summary>
    /// <value>
    /// The time span after which newly generated tokens expire. 
    /// Default is 1 hour. Must be positive and reasonable for the use case.
    /// </value>
    /// <remarks>
    /// <para>
    /// Token expiration is a critical security feature that limits the window
    /// of vulnerability if a token is compromised. The expiration time should
    /// balance security concerns with user experience.
    /// </para>
    /// <para>
    /// Considerations for setting expiration time:
    /// - User tokens: 15 minutes to 24 hours depending on sensitivity
    /// - API tokens: 1 hour to 7 days depending on use case
    /// - Service tokens: Can be longer for background processes
    /// - Consider implementing refresh token patterns for longer sessions
    /// </para>
    /// <para>
    /// The 'exp' claim in generated tokens will be set to the current time
    /// plus this duration, unless explicitly overridden during token generation.
    /// </para>
    /// </remarks>
    /// <example>
    /// Common values: TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromDays(1)
    /// </example>
    public required TimeSpan DefaultExpiration { get; init; }

    /// <summary>
    /// Gets or sets the default clock skew tolerance for JWT token validation.
    /// </summary>
    /// <value>
    /// The time span of acceptable clock difference between token issuer and validator.
    /// Default is 5 minutes. Should be minimal but accommodate realistic clock drift.
    /// </value>
    /// <remarks>
    /// <para>
    /// Clock skew tolerance accounts for time differences between distributed systems
    /// due to clock synchronization issues, network delays, and system clock drift.
    /// This prevents valid tokens from being rejected due to minor time discrepancies.
    /// </para>
    /// <para>
    /// The clock skew is applied to both:
    /// - 'nbf' (not before) claim validation - token becomes valid this amount earlier
    /// - 'exp' (expiration) claim validation - token remains valid this amount longer
    /// </para>
    /// <para>
    /// Security considerations:
    /// - Keep as small as reasonably possible (typically 1-10 minutes)
    /// - Monitor system clocks and implement NTP synchronization
    /// - Consider security implications of extended validity windows
    /// - Document the configured tolerance for security audits
    /// </para>
    /// <para>
    /// This value is used as the default for token validation operations unless
    /// explicitly overridden in validation parameters.
    /// </para>
    /// </remarks>
    /// <example>
    /// Typical values: TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)
    /// </example>
    public required TimeSpan ClockSkew { get; init; }
}
