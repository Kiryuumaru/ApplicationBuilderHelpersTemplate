namespace Application.Cloud.Router.Grid.Models;

/// <summary>
/// Configuration options for GridRouter.
/// </summary>
public sealed class GridRouterOptions
{
    /// <summary>
    /// Heartbeat interval for health checking. Default: 30 seconds.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Time after which a node is considered dead if no heartbeat. Default: 90 seconds.
    /// </summary>
    public TimeSpan NodeTimeout { get; init; } = TimeSpan.FromSeconds(90);
}
