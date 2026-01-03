namespace Presentation.WebApi.Models.Responses;

/// <summary>
/// Response for the revoke all sessions operation.
/// </summary>
public sealed record SessionRevokeAllResponse
{
    /// <summary>
    /// Gets or sets the number of sessions that were revoked.
    /// </summary>
    public required int RevokedCount { get; init; }
}
