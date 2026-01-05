namespace Presentation.WebApi.Models.Responses;

/// <summary>
/// Response containing the list of all available roles.
/// </summary>
public sealed class RoleListResponse
{
    /// <summary>
    /// Gets or sets the list of available roles.
    /// </summary>
    public required IReadOnlyList<RoleInfoResponse> Roles { get; init; }
}
