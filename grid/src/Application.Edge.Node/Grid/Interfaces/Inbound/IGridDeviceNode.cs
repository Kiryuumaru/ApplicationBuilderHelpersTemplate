namespace Application.Edge.Node.Grid.Interfaces.Inbound;

/// <summary>
/// Grid device node service for edge devices connecting to cloud.
/// </summary>
public interface IGridDeviceNode : IAsyncDisposable
{
    /// <summary>
    /// Unique device identifier.
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// True if connected to cloud node.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to the cloud node.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the cloud node.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends data to the cloud.
    /// </summary>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when connected to cloud node.
    /// </summary>
    event Action? Connected;

    /// <summary>
    /// Event raised when disconnected from cloud node.
    /// </summary>
    event Action? Disconnected;

    /// <summary>
    /// Event raised when a message is received from cloud.
    /// </summary>
    event Action<ReadOnlyMemory<byte>>? MessageReceived;
}
