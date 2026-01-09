using System;

namespace Domain.Identity.Models;

/// <summary>
/// Represents an API key for programmatic access.
/// The actual key is a api-key token type - we only store metadata for revocation tracking.
/// </summary>
public sealed class ApiKey
{
    /// <summary>
    /// Primary key (GUID). Also embedded in the token as the 'jti' claim.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The user who owns this API key.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// User-friendly name for the API key (e.g., "Trading Bot", "CI/CD Pipeline").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// When the API key was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the API key expires. Null means never expires.
    /// Also baked into the JWT 'exp' claim.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// When the API key was last used for authentication.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Whether this API key has been revoked.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// When the API key was revoked. Null if not revoked.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
