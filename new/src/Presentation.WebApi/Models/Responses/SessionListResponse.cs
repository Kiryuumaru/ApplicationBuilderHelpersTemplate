namespace Presentation.WebApi.Models.Responses;

/// <summary>
/// Response containing a list of active sessions.
/// </summary>
public sealed record SessionListResponse
{
    /// <summary>
    /// Gets or sets the list of active sessions.
    /// </summary>
    public required IReadOnlyCollection<SessionInfoResponse> Sessions { get; init; }
}
