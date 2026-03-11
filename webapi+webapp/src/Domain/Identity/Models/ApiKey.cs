using System;

namespace Domain.Identity.Models;

public sealed class ApiKey
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public required string Name { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public bool IsRevoked { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public void RecordUsage()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
    }

    public void Revoke()
    {
        if (IsRevoked)
        {
            return;
        }

        IsRevoked = true;
        RevokedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Reconstructs an ApiKey from persistence data.
    /// </summary>
    public static ApiKey Reconstruct(
        Guid id,
        Guid userId,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt,
        DateTimeOffset? lastUsedAt,
        bool isRevoked,
        DateTimeOffset? revokedAt)
    {
        return new ApiKey
        {
            Id = id,
            UserId = userId,
            Name = name,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            LastUsedAt = lastUsedAt,
            IsRevoked = isRevoked,
            RevokedAt = revokedAt
        };
    }
}
