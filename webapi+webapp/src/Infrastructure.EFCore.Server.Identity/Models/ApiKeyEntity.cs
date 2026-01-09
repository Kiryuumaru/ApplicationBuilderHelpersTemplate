namespace Infrastructure.EFCore.Server.Identity.Models;

/// <summary>
/// Entity for storing API key metadata.
/// The actual key is a JWT - we only store metadata for revocation tracking.
/// </summary>
public sealed class ApiKeyEntity
{
    /// <summary>
    /// Primary key (GUID). Also embedded in the JWT as the 'jti' claim.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// The user who owns this API key.
    /// </summary>
    public required Guid UserId { get; set; }

    /// <summary>
    /// User-friendly name for the API key (e.g., "Trading Bot", "CI/CD Pipeline").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// When the API key was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the API key expires. Null means never expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// When the API key was last used for authentication.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Whether this API key has been revoked.
    /// </summary>
    public required bool IsRevoked { get; set; }

    /// <summary>
    /// When the API key was revoked. Null if not revoked.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
