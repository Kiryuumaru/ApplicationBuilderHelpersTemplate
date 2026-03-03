namespace Domain.Identity.Models;

public class ApiKey
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public bool IsRevoked { get; private set; }
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
