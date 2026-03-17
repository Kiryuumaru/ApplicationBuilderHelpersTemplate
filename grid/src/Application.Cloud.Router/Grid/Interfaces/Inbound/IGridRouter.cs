namespace Application.Cloud.Router.Grid.Interfaces.Inbound;

/// <summary>
/// Grid router service that manages cloud nodes and routes messages to devices.
/// </summary>
public interface IGridRouter : IAsyncDisposable
{
    /// <summary>
    /// Starts the router service.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the router service.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends data to a specific device.
    /// </summary>
    Task SendToDeviceAsync(string deviceId, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts data to all connected devices.
    /// </summary>
    Task BroadcastToAllDevicesAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the specified device is connected.
    /// </summary>
    bool IsDeviceConnected(string deviceId);

    /// <summary>
    /// Gets a list of all connected device IDs.
    /// </summary>
    IReadOnlyList<string> GetConnectedDeviceIds();

    /// <summary>
    /// Gets a list of all connected cloud node IDs.
    /// </summary>
    IReadOnlyList<string> GetConnectedCloudNodes();

    /// <summary>
    /// Event raised when a device connects (via any cloud node).
    /// </summary>
    event Action<string>? DeviceConnected;

    /// <summary>
    /// Event raised when a device disconnects.
    /// </summary>
    event Action<string>? DeviceDisconnected;

    /// <summary>
    /// Event raised when a cloud node connects.
    /// </summary>
    event Action<string>? CloudNodeConnected;

    /// <summary>
    /// Event raised when a cloud node disconnects.
    /// </summary>
    event Action<string>? CloudNodeDisconnected;

    /// <summary>
    /// Event raised when a message is received from a device.
    /// </summary>
    event Action<string, ReadOnlyMemory<byte>>? DeviceMessageReceived;

    /// <summary>
    /// Handles an incoming cloud node WebSocket connection.
    /// </summary>
    Task HandleNodeConnectionAsync(System.Net.WebSockets.WebSocket webSocket, CancellationToken cancellationToken = default);
}
