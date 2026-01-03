namespace Domain.Identity.Models;

/// <summary>
/// Represents a user's login session with refresh token tracking.
/// Sessions enable logout-everywhere functionality and token rotation for security.
/// </summary>
public sealed class LoginSession
{
    /// <summary>
    /// Gets the unique identifier for this session.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the ID of the user who owns this session.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the hash of the current refresh token for this session.
    /// Used to validate refresh requests and detect token theft.
    /// </summary>
    public string RefreshTokenHash { get; private set; }

    /// <summary>
    /// Gets the name of the device or browser used to create this session.
    /// </summary>
    public string? DeviceName { get; private set; }

    /// <summary>
    /// Gets the user agent string from the request that created this session.
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Gets the IP address from which this session was created.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// Gets the timestamp when this session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when this session was last used (token refreshed).
    /// </summary>
    public DateTimeOffset LastUsedAt { get; private set; }

    /// <summary>
    /// Gets the timestamp when this session expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// Gets whether this session has been revoked.
    /// </summary>
    public bool IsRevoked { get; private set; }

    /// <summary>
    /// Gets the timestamp when this session was revoked, if applicable.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    // Required for EF Core
    private LoginSession()
    {
        RefreshTokenHash = string.Empty;
    }

    private LoginSession(
        Guid id,
        Guid userId,
        string refreshTokenHash,
        string? deviceName,
        string? userAgent,
        string? ipAddress,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        RefreshTokenHash = refreshTokenHash;
        DeviceName = deviceName;
        UserAgent = userAgent;
        IpAddress = ipAddress;
        CreatedAt = createdAt;
        LastUsedAt = createdAt;
        ExpiresAt = expiresAt;
        IsRevoked = false;
    }

    /// <summary>
    /// Creates a new login session.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="refreshTokenHash">Hash of the refresh token.</param>
    /// <param name="expiresAt">When the session expires.</param>
    /// <param name="deviceName">Optional device name.</param>
    /// <param name="userAgent">Optional user agent string.</param>
    /// <param name="ipAddress">Optional IP address.</param>
    /// <returns>A new login session.</returns>
    public static LoginSession Create(
        Guid userId,
        string refreshTokenHash,
        DateTimeOffset expiresAt,
        string? deviceName = null,
        string? userAgent = null,
        string? ipAddress = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(refreshTokenHash))
        {
            throw new ArgumentException("Refresh token hash cannot be empty.", nameof(refreshTokenHash));
        }

        return new LoginSession(
            Guid.NewGuid(),
            userId,
            refreshTokenHash,
            deviceName,
            userAgent,
            ipAddress,
            DateTimeOffset.UtcNow,
            expiresAt);
    }

    /// <summary>
    /// Reconstructs a login session from persisted data.
    /// </summary>
    public static LoginSession Reconstruct(
        Guid id,
        Guid userId,
        string refreshTokenHash,
        string? deviceName,
        string? userAgent,
        string? ipAddress,
        DateTimeOffset createdAt,
        DateTimeOffset lastUsedAt,
        DateTimeOffset expiresAt,
        bool isRevoked,
        DateTimeOffset? revokedAt)
    {
        var session = new LoginSession(
            id,
            userId,
            refreshTokenHash,
            deviceName,
            userAgent,
            ipAddress,
            createdAt,
            expiresAt)
        {
            LastUsedAt = lastUsedAt,
            IsRevoked = isRevoked,
            RevokedAt = revokedAt
        };
        return session;
    }

    /// <summary>
    /// Updates the refresh token hash (for token rotation).
    /// </summary>
    /// <param name="newRefreshTokenHash">The new refresh token hash.</param>
    /// <param name="newExpiresAt">The new expiration time.</param>
    public void RotateRefreshToken(string newRefreshTokenHash, DateTimeOffset newExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(newRefreshTokenHash))
        {
            throw new ArgumentException("New refresh token hash cannot be empty.", nameof(newRefreshTokenHash));
        }

        RefreshTokenHash = newRefreshTokenHash;
        LastUsedAt = DateTimeOffset.UtcNow;
        ExpiresAt = newExpiresAt;
    }

    /// <summary>
    /// Marks this session as revoked.
    /// </summary>
    public void Revoke()
    {
        IsRevoked = true;
        RevokedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Checks if this session is still valid (not expired and not revoked).
    /// </summary>
    public bool IsValid => !IsRevoked && ExpiresAt > DateTimeOffset.UtcNow;
}
