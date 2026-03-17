namespace Application.Cloud.Node.Grid.Interfaces.Inbound;

/// <summary>
/// Grid cloud node service that aggregates device connections.
/// </summary>
public interface IGridCloudNode : IAsyncDisposable
{
    /// <summary>
    /// Unique node identifier.
    /// </summary>
    string NodeId { get; }

    /// <summary>
    /// True if connected to router.
    /// </summary>
    bool IsConnectedToRouter { get; }

    /// <summary>
    /// Current number of connected devices.
    /// </summary>
    int ConnectedDeviceCount { get; }

    /// <summary>
    /// Starts the cloud node service.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the cloud node service.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when a device connects.
    /// </summary>
    event Action<string>? DeviceConnected;

    /// <summary>
    /// Event raised when a device disconnects.
    /// </summary>
    event Action<string>? DeviceDisconnected;

    /// <summary>
    /// Event raised when a message is received from a device.
    /// </summary>
    event Action<string, ReadOnlyMemory<byte>>? DeviceMessageReceived;

    /// <summary>
    /// Handles an incoming device WebSocket connection.
    /// </summary>
    Task HandleDeviceConnectionAsync(System.Net.WebSockets.WebSocket webSocket, CancellationToken cancellationToken = default);
}
