namespace Presentation.WebApi.Models.Responses;

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
