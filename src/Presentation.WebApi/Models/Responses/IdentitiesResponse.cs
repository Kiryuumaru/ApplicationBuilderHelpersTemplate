namespace Presentation.WebApi.Models.Responses;

/// <summary>
/// Response containing the user's linked identities.
/// </summary>
public sealed record IdentitiesResponse
{
    /// <summary>
    /// Whether the user is anonymous (no linked identities).
    /// </summary>
    public required bool IsAnonymous { get; init; }

    /// <summary>
    /// When the user upgraded from anonymous (linked their first identity).
    /// </summary>
    public DateTimeOffset? LinkedAt { get; init; }

    /// <summary>
    /// Whether the user has a password linked.
    /// </summary>
    public required bool HasPassword { get; init; }

    /// <summary>
    /// The user's email address (if linked).
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Whether the email is confirmed.
    /// </summary>
    public bool EmailConfirmed { get; init; }

    /// <summary>
    /// The linked OAuth providers.
    /// </summary>
    public required IReadOnlyCollection<LinkedProviderInfo> LinkedProviders { get; init; }

    /// <summary>
    /// The user's linked passkeys.
    /// </summary>
    public required IReadOnlyCollection<LinkedPasskeyInfo> LinkedPasskeys { get; init; }
}

/// <summary>
/// Information about a linked OAuth provider.
/// </summary>
public sealed record LinkedProviderInfo
{
    /// <summary>
    /// The OAuth provider name (e.g., "google", "github").
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Display name from the provider (e.g., user's name or email).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Email from the provider (if available).
    /// </summary>
    public string? Email { get; init; }
}

/// <summary>
/// Information about a linked passkey.
/// </summary>
public sealed record LinkedPasskeyInfo
{
    /// <summary>
    /// The passkey's unique identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The passkey's display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// When the passkey was registered.
    /// </summary>
    public required DateTimeOffset RegisteredAt { get; init; }
}
