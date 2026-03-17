using System.Collections.Concurrent;
using System.Net.WebSockets;
using Application.Cloud.Node.Grid.Interfaces.Inbound;
using Application.Cloud.Node.Grid.Models;
using Domain.Grid.Enums;
using Domain.Grid.Models;
using Infrastructure.NetConduit.Serialization;
using Microsoft.Extensions.Logging;
using NetConduit;
using NetConduit.Transits;
using NetConduit.WebSocket;

namespace Infrastructure.NetConduit.Cloud.Node.Adapters;

/// <summary>
/// Grid cloud node implementation using NetConduit WebSocket with DeltaTransit.
/// Connects to router and accepts device connections.
/// </summary>
internal sealed class GridCloudNode : IGridCloudNode
{
    private readonly GridCloudOptions _options;
    private readonly ILogger<GridCloudNode> _logger;
    private readonly ConcurrentDictionary<string, DeviceConnection> _devices = new();

    private IStreamMultiplexer? _routerMux;
    private DeltaTransit<GridMessage>? _routerTransit;
    private CancellationTokenSource? _runCts;
    private Task? _routerReceiveTask;
    private volatile bool _isConnectedToRouter;
    private bool _disposed;

    public GridCloudNode(GridCloudOptions options, ILogger<GridCloudNode> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string NodeId { get; } = Guid.NewGuid().ToString("N")[..8];
    public bool IsConnectedToRouter => _isConnectedToRouter;
    public int ConnectedDeviceCount => _devices.Count;

    public event Action<string>? DeviceConnected;
    public event Action<string>? DeviceDisconnected;
    public event Action<string, ReadOnlyMemory<byte>>? DeviceMessageReceived;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GridCloudNode));

        _logger.LogInformation("Starting cloud node {NodeId}, connecting to router at {Endpoint}",
            NodeId, _options.RouterEndpoint);

        _runCts = new CancellationTokenSource();

        // Connect to router
        await ConnectToRouterAsync(cancellationToken);
    }

    private async Task ConnectToRouterAsync(CancellationToken cancellationToken)
    {
        // Create options with defaults - EnableReconnection is true by default
        var muxOptions = WebSocketMultiplexer.CreateOptions(_options.RouterEndpoint);

        _routerMux = StreamMultiplexer.Create(muxOptions);

        _routerMux.OnDisconnected += (reason, ex) =>
        {
            _isConnectedToRouter = false;
            _logger.LogWarning("Cloud node {NodeId} disconnected from router: {Reason}", NodeId, reason);
        };

        // Note: OnReconnected event handled via monitoring state changes
        // Reconnection is automatic when EnableReconnection is true (default)

        _ = _routerMux.Start();
        await _routerMux.WaitForReadyAsync(cancellationToken);

        _routerTransit = await _routerMux.OpenDeltaTransitAsync(
            "node-control",
            NetConduitJsonContext.Default.GridMessage,
            cancellationToken: cancellationToken);

        await RegisterWithRouterAsync(cancellationToken);

        _routerReceiveTask = ReceiveFromRouterAsync(_runCts?.Token ?? CancellationToken.None);
    }

    private async Task RegisterWithRouterAsync(CancellationToken cancellationToken)
    {
        if (_routerTransit is null)
            return;

        var registerMsg = GridMessage.CreateNodeRegister(NodeId);
        await _routerTransit.SendAsync(registerMsg, cancellationToken);

        var response = await _routerTransit.ReceiveAsync(cancellationToken);
        if (response?.Type == GridMessageType.NodeRegisterAck && response.ErrorCode == GridErrorCode.None)
        {
            _isConnectedToRouter = true;
            _logger.LogInformation("Cloud node {NodeId} registered with router", NodeId);

            // Re-register all connected devices
            foreach (var deviceId in _devices.Keys)
            {
                await _routerTransit.SendAsync(GridMessage.CreateDeviceConnected(deviceId), cancellationToken);
            }
        }
        else
        {
            _logger.LogError("Cloud node registration failed: {ErrorCode}", response?.ErrorCode);
        }
    }

    private async Task ReceiveFromRouterAsync(CancellationToken cancellationToken)
    {
        if (_routerTransit is null)
            return;

        try
        {
            await foreach (var message in _routerTransit.ReceiveAllAsync(cancellationToken))
            {
                await HandleRouterMessageAsync(message, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving from router");
        }
    }

    private async Task HandleRouterMessageAsync(GridMessage message, CancellationToken cancellationToken)
    {
        switch (message.Type)
        {
            case GridMessageType.SendToDevice:
                if (message.TargetId is not null && message.Payload is not null)
                {
                    await SendToDeviceAsync(message.TargetId, message.Payload, cancellationToken);
                }
                break;

            case GridMessageType.Ping:
                if (_routerTransit is not null)
                {
                    await _routerTransit.SendAsync(GridMessage.CreatePong(), cancellationToken);
                }
                break;

            default:
                _logger.LogDebug("Received router message type {Type}", message.Type);
                break;
        }
    }

    /// <summary>
    /// Handles an incoming device WebSocket connection.
    /// Called by Presentation layer when a device connects.
    /// </summary>
    public async Task HandleDeviceConnectionAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        string? deviceId = null;

        try
        {
            var muxOptions = WebSocketMultiplexer.CreateServerOptions(webSocket);
            var mux = StreamMultiplexer.Create(muxOptions);
            _ = mux.Start();
            await mux.WaitForReadyAsync(cancellationToken);

            var transit = await mux.AcceptDeltaTransitAsync(
                "control",
                NetConduitJsonContext.Default.GridMessage,
                cancellationToken: cancellationToken);

            // Wait for device registration
            var registerMsg = await transit.ReceiveAsync(cancellationToken);
            if (registerMsg?.Type != GridMessageType.DeviceRegister || registerMsg.SourceId is null)
            {
                await transit.SendAsync(GridMessage.CreateDeviceRegisterAck("", false, GridErrorCode.InvalidMessage), cancellationToken);
                return;
            }

            deviceId = registerMsg.SourceId;

            // Check for duplicate device ID
            if (_devices.ContainsKey(deviceId))
            {
                await transit.SendAsync(GridMessage.CreateDeviceRegisterAck(deviceId, false, GridErrorCode.DeviceIdCollision), cancellationToken);
                _logger.LogWarning("Device registration rejected: duplicate ID {DeviceId}", deviceId);
                return;
            }

            var connection = new DeviceConnection(deviceId, mux, transit);
            if (!_devices.TryAdd(deviceId, connection))
            {
                await transit.SendAsync(GridMessage.CreateDeviceRegisterAck(deviceId, false, GridErrorCode.DeviceIdCollision), cancellationToken);
                return;
            }

            // Acknowledge registration
            await transit.SendAsync(GridMessage.CreateDeviceRegisterAck(deviceId, true), cancellationToken);
            _logger.LogInformation("Device {DeviceId} registered with cloud node {NodeId}", deviceId, NodeId);

            // Notify router
            if (_routerTransit is not null && _isConnectedToRouter)
            {
                await _routerTransit.SendAsync(GridMessage.CreateDeviceConnected(deviceId), cancellationToken);
            }

            DeviceConnected?.Invoke(deviceId);

            // Handle device messages
            await HandleDeviceMessagesAsync(connection, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error handling device connection {DeviceId}", deviceId ?? "unknown");
        }
        finally
        {
            if (deviceId is not null && _devices.TryRemove(deviceId, out var conn))
            {
                await conn.DisposeAsync();

                // Notify router
                if (_routerTransit is not null && _isConnectedToRouter)
                {
                    try
                    {
                        await _routerTransit.SendAsync(GridMessage.CreateDeviceDisconnected(deviceId), CancellationToken.None);
                    }
                    catch
                    {
                        // Ignore errors during disconnect notification
                    }
                }

                _logger.LogInformation("Device {DeviceId} disconnected from cloud node {NodeId}", deviceId, NodeId);
                DeviceDisconnected?.Invoke(deviceId);
            }
        }
    }

    private async Task HandleDeviceMessagesAsync(DeviceConnection connection, CancellationToken cancellationToken)
    {
        await foreach (var message in connection.Transit.ReceiveAllAsync(cancellationToken))
        {
            if (message.Type == GridMessageType.Data && message.Payload is not null)
            {
                DeviceMessageReceived?.Invoke(connection.DeviceId, message.Payload);

                // Forward to router
                if (_routerTransit is not null && _isConnectedToRouter)
                {
                    await _routerTransit.SendAsync(
                        GridMessage.CreateDeviceMessage(connection.DeviceId, message.Payload),
                        cancellationToken);
                }
            }
            else if (message.Type == GridMessageType.Ping)
            {
                await connection.Transit.SendAsync(GridMessage.CreatePong(), cancellationToken);
            }
        }
    }

    private async Task SendToDeviceAsync(string deviceId, byte[] payload, CancellationToken cancellationToken)
    {
        if (_devices.TryGetValue(deviceId, out var connection))
        {
            var message = GridMessage.CreateSendToDevice(deviceId, payload);
            await connection.Transit.SendAsync(message, cancellationToken);
        }
        else
        {
            _logger.LogWarning("Device {DeviceId} not found on this node", deviceId);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _runCts?.Cancel();

        // Disconnect all devices
        foreach (var kvp in _devices)
        {
            await kvp.Value.DisposeAsync();
        }
        _devices.Clear();

        // Wait for router receive task
        if (_routerReceiveTask is not null)
        {
            try
            {
                await _routerReceiveTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Router receive task did not complete in time");
            }
        }

        // Disconnect from router
        if (_routerTransit is not null)
        {
            await _routerTransit.DisposeAsync();
            _routerTransit = null;
        }

        if (_routerMux is not null)
        {
            await _routerMux.DisposeAsync();
            _routerMux = null;
        }

        _isConnectedToRouter = false;
        _logger.LogInformation("Cloud node {NodeId} stopped", NodeId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopAsync();
        _runCts?.Dispose();
    }

    private sealed class DeviceConnection(string deviceId, IStreamMultiplexer mux, DeltaTransit<GridMessage> transit) : IAsyncDisposable
    {
        public string DeviceId => deviceId;
        public IStreamMultiplexer Mux => mux;
        public DeltaTransit<GridMessage> Transit => transit;

        public async ValueTask DisposeAsync()
        {
            await Transit.DisposeAsync();
            await Mux.DisposeAsync();
        }
    }
}
