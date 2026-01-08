namespace Presentation.WebApi.Controllers.V1.Iam.UsersController.Responses;

/// <summary>
/// Response containing a list of users.
/// </summary>
public sealed class UserListResponse
{
    /// <summary>
    /// Gets or sets the list of users.
    /// </summary>
    public required IReadOnlyCollection<UserResponse> Users { get; init; }

    /// <summary>
    /// Gets or sets the total number of users.
    /// </summary>
    public int Total { get; init; }
}
