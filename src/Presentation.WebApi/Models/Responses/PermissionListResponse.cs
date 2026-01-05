namespace Presentation.WebApi.Models.Responses;

/// <summary>
/// Response containing the list of all available permissions.
/// </summary>
public sealed class PermissionListResponse
{
    /// <summary>
    /// Gets or sets the permission tree roots.
    /// </summary>
    public required IReadOnlyList<PermissionInfoResponse> Permissions { get; init; }
}
