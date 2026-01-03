namespace Presentation.WebApi.Models.Responses;

/// <summary>
/// Information about a linked external login.
/// </summary>
public sealed record ExternalLoginResponse
{
    /// <summary>
    /// The provider name.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Display name for the linked account.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Email from the linked account.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// When this login was linked.
    /// </summary>
    public required DateTimeOffset LinkedAt { get; init; }
}

/// <summary>
/// Response containing list of linked external logins.
/// </summary>
public sealed record ExternalLoginsResponse
{
    /// <summary>
    /// List of linked external logins.
    /// </summary>
    public required IReadOnlyCollection<ExternalLoginResponse> Logins { get; init; }
}
