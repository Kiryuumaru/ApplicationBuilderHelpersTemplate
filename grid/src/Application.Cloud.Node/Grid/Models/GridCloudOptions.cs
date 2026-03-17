namespace Application.Cloud.Node.Grid.Models;

/// <summary>
/// Configuration options for GridCloudNode.
/// </summary>
public sealed class GridCloudOptions
{
    /// <summary>
    /// Unique identifier for this cloud node. Auto-generated if not specified.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// Router WebSocket endpoint to connect to.
    /// </summary>
    public required string RouterEndpoint { get; init; }

    /// <summary>
    /// Heartbeat interval for router connection. Default: 30 seconds.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay before reconnecting to router. Default: 5 seconds.
    /// </summary>
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);
}
