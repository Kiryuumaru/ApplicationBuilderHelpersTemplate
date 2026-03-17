namespace Application.Edge.Node.Grid.Models;

/// <summary>
/// Configuration options for GridDeviceNode.
/// </summary>
public sealed class GridDeviceOptions
{
    /// <summary>
    /// Unique device identifier.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Cloud node WebSocket endpoint to connect to.
    /// </summary>
    public required string CloudNodeEndpoint { get; init; }

    /// <summary>
    /// Delay between reconnect attempts. Default: 5 seconds.
    /// </summary>
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Keep-alive heartbeat interval. Default: 30 seconds.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);
}
