namespace Domain.Identity.Models;

/// <summary>
/// Represents an API key for programmatic access.
/// The actual key is a api-key token type - we only store metadata for revocation tracking.
/// </summary>
public class ApiKey
{
    /// <summary>
    /// Primary key (GUID). Also embedded in the token as the 'jti' claim.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// The user who owns this API key.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// User-friendly name for the API key (e.g., "Trading Bot", "CI/CD Pipeline").
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// When the API key was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// When the API key expires. Null means never expires.
    /// Also baked into the JWT 'exp' claim.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// When the API key was last used for authentication.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>
    /// Whether this API key has been revoked.
    /// </summary>
    public bool IsRevoked { get; private set; }

    /// <summary>
    /// When the API key was revoked. Null if not revoked.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    protected ApiKey(Guid id, Guid userId, string name, DateTimeOffset createdAt, DateTimeOffset? expiresAt)
    {
        Id = id;
        UserId = userId;
        Name = name;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public static ApiKey Create(Guid userId, string name, DateTimeOffset? expiresAt = null)
    {
        return new ApiKey(Guid.NewGuid(), userId, name, DateTimeOffset.UtcNow, expiresAt);
    }

    public static ApiKey Hydrate(
        Guid id,
        Guid userId,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt,
        DateTimeOffset? lastUsedAt,
        bool isRevoked,
        DateTimeOffset? revokedAt)
    {
        var apiKey = new ApiKey(id, userId, name, createdAt, expiresAt)
        {
            LastUsedAt = lastUsedAt,
            IsRevoked = isRevoked,
            RevokedAt = revokedAt
        };
        return apiKey;
    }

    public void MarkUsed(DateTimeOffset usedAt)
    {
        LastUsedAt = usedAt;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        IsRevoked = true;
        RevokedAt = revokedAt;
    }
}
