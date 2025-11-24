using Domain.Shared.Exceptions;
using Domain.Shared.Models;

namespace Domain.Identity.ValueObjects;

public sealed class PasswordCredential : ValueObject
{
    public string Algorithm { get; }
    public string Hash { get; }
    public string Salt { get; }
    public int IterationCount { get; }
    public DateTimeOffset Created { get; }
    public DateTimeOffset? ExpiresAt { get; }

    private PasswordCredential(string algorithm, string hash, string salt, int iterationCount, DateTimeOffset created, DateTimeOffset? expiresAt)
    {
        Algorithm = Normalize(algorithm, nameof(algorithm));
        Hash = Normalize(hash, nameof(hash));
        Salt = Normalize(salt, nameof(salt));
        IterationCount = iterationCount > 0 ? iterationCount : throw new DomainException("Iteration count must be positive.");
        Created = created;
        ExpiresAt = expiresAt;
    }

    public static PasswordCredential Create(string algorithm, string hash, string salt, int iterationCount, DateTimeOffset? created = null, DateTimeOffset? expiresAt = null)
    {
        var timestamp = created ?? DateTimeOffset.UtcNow;
        if (expiresAt is not null && expiresAt <= timestamp)
        {
            throw new DomainException("Credential expiration must be greater than the creation timestamp.");
        }

        return new PasswordCredential(algorithm, hash, salt, iterationCount, timestamp, expiresAt);
    }

    public bool IsExpired(DateTimeOffset timestamp) => ExpiresAt is not null && timestamp >= ExpiresAt;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Algorithm;
        yield return Hash;
        yield return Salt;
        yield return IterationCount;
        yield return Created;
        yield return ExpiresAt ?? DateTimeOffset.MinValue;
    }

    private static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{parameterName} cannot be null or empty.");
        }

        return value.Trim();
    }
}
