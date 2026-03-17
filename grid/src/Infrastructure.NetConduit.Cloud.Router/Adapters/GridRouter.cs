using System.Collections.Concurrent;
using System.Net.WebSockets;
using Application.Cloud.Router.Grid.Interfaces.Inbound;
using Application.Cloud.Router.Grid.Models;
using Domain.Grid.Enums;
using Domain.Grid.Models;
using Infrastructure.NetConduit.Serialization;
using Microsoft.Extensions.Logging;
using NetConduit;
using NetConduit.Transits;
using NetConduit.WebSocket;

namespace Infrastructure.NetConduit.Cloud.Router.Adapters;

/// <summary>
/// Grid router implementation using NetConduit WebSocket with DeltaTransit.
/// Manages cloud nodes and routes messages to devices.
/// </summary>
internal sealed class GridRouter : IGridRouter
{
    private readonly GridRouterOptions _options;
    private readonly ILogger<GridRouter> _logger;
    private readonly ConcurrentDictionary<string, CloudNodeConnection> _nodes = new();
    private readonly ConcurrentDictionary<string, string> _deviceRegistry = new(); // deviceId → nodeId

    private CancellationTokenSource? _runCts;
    private bool _disposed;

    public GridRouter(GridRouterOptions options, ILogger<GridRouter> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event Action<string>? DeviceConnected;
    public event Action<string>? DeviceDisconnected;
    public event Action<string>? CloudNodeConnected;
    public event Action<string>? CloudNodeDisconnected;
    public event Action<string, ReadOnlyMemory<byte>>? DeviceMessageReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GridRouter));

        _runCts = new CancellationTokenSource();
        _logger.LogInformation("Grid router started");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles an incoming cloud node WebSocket connection.
    /// Called by Presentation layer when a cloud node connects.
    /// </summary>
    public async Task HandleNodeConnectionAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        string? nodeId = null;

        try
        {
            var muxOptions = WebSocketMultiplexer.CreateServerOptions(webSocket);
            var mux = StreamMultiplexer.Create(muxOptions);
            _ = mux.Start();
            await mux.WaitForReadyAsync(cancellationToken);

            var transit = await mux.AcceptDeltaTransitAsync(
                "node-control",
                NetConduitJsonContext.Default.GridMessage,
                cancellationToken: cancellationToken);

            // Wait for node registration
            var registerMsg = await transit.ReceiveAsync(cancellationToken);
            if (registerMsg?.Type != GridMessageType.NodeRegister || registerMsg.SourceId is null)
            {
                await transit.SendAsync(GridMessage.CreateNodeRegisterAck("", false), cancellationToken);
                return;
            }

            nodeId = registerMsg.SourceId;

            // Check for duplicate node ID
            if (_nodes.ContainsKey(nodeId))
            {
                await transit.SendAsync(GridMessage.CreateNodeRegisterAck(nodeId, false), cancellationToken);
                _logger.LogWarning("Cloud node registration rejected: duplicate ID {NodeId}", nodeId);
                return;
            }

            var connection = new CloudNodeConnection(nodeId, mux, transit);
            if (!_nodes.TryAdd(nodeId, connection))
            {
                await transit.SendAsync(GridMessage.CreateNodeRegisterAck(nodeId, false), cancellationToken);
                return;
            }

            // Acknowledge registration
            await transit.SendAsync(GridMessage.CreateNodeRegisterAck(nodeId, true), cancellationToken);
            _logger.LogInformation("Cloud node {NodeId} registered with router", nodeId);

            CloudNodeConnected?.Invoke(nodeId);

            // Handle node messages
            await HandleNodeMessagesAsync(connection, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error handling cloud node connection {NodeId}", nodeId ?? "unknown");
        }
        finally
        {
            if (nodeId is not null && _nodes.TryRemove(nodeId, out var conn))
            {
                await conn.DisposeAsync();

                // Remove all devices registered to this node
                var devicesToRemove = _deviceRegistry
                    .Where(kvp => kvp.Value == nodeId)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var deviceId in devicesToRemove)
                {
                    if (_deviceRegistry.TryRemove(deviceId, out _))
                    {
                        _logger.LogInformation("Device {DeviceId} removed (node {NodeId} disconnected)", deviceId, nodeId);
                        DeviceDisconnected?.Invoke(deviceId);
                    }
                }

                _logger.LogInformation("Cloud node {NodeId} disconnected from router", nodeId);
                CloudNodeDisconnected?.Invoke(nodeId);
            }
        }
    }

    private async Task HandleNodeMessagesAsync(CloudNodeConnection connection, CancellationToken cancellationToken)
    {
        await foreach (var message in connection.Transit.ReceiveAllAsync(cancellationToken))
        {
            switch (message.Type)
            {
                case GridMessageType.DeviceConnected:
                    if (message.SourceId is not null)
                    {
                        _deviceRegistry[message.SourceId] = connection.NodeId;
                        _logger.LogInformation("Device {DeviceId} registered via node {NodeId}", message.SourceId, connection.NodeId);
                        DeviceConnected?.Invoke(message.SourceId);
                    }
                    break;

                case GridMessageType.DeviceDisconnected:
                    if (message.SourceId is not null && _deviceRegistry.TryRemove(message.SourceId, out _))
                    {
                        _logger.LogInformation("Device {DeviceId} unregistered from node {NodeId}", message.SourceId, connection.NodeId);
                        DeviceDisconnected?.Invoke(message.SourceId);
                    }
                    break;

                case GridMessageType.DeviceMessage:
                    if (message.SourceId is not null && message.Payload is not null)
                    {
                        DeviceMessageReceived?.Invoke(message.SourceId, message.Payload);
                    }
                    break;

                case GridMessageType.Ping:
                    await connection.Transit.SendAsync(GridMessage.CreatePong(), cancellationToken);
                    break;

                default:
                    _logger.LogDebug("Received node message type {Type} from {NodeId}", message.Type, connection.NodeId);
                    break;
            }
        }
    }

    public async Task SendToDeviceAsync(string deviceId, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (!_deviceRegistry.TryGetValue(deviceId, out var nodeId))
        {
            throw new InvalidOperationException($"Device {deviceId} not found");
        }

        if (!_nodes.TryGetValue(nodeId, out var connection))
        {
            throw new InvalidOperationException($"Node {nodeId} not connected");
        }

        var message = GridMessage.CreateSendToDevice(deviceId, data.ToArray());
        await connection.Transit.SendAsync(message, cancellationToken);
    }

    public async Task BroadcastToAllDevicesAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var payload = data.ToArray();
        var tasks = new List<Task>();

        foreach (var nodeId in _nodes.Keys)
        {
            var deviceIds = _deviceRegistry
                .Where(kvp => kvp.Value == nodeId)
                .Select(kvp => kvp.Key)
                .ToList();

            if (_nodes.TryGetValue(nodeId, out var connection))
            {
                foreach (var deviceId in deviceIds)
                {
                    var message = GridMessage.CreateSendToDevice(deviceId, payload);
                    tasks.Add(connection.Transit.SendAsync(message, cancellationToken).AsTask());
                }
            }
        }

        await Task.WhenAll(tasks);
    }

    public bool IsDeviceConnected(string deviceId)
    {
        return _deviceRegistry.ContainsKey(deviceId);
    }

    public IReadOnlyList<string> GetConnectedDeviceIds()
    {
        return _deviceRegistry.Keys.ToList();
    }

    public IReadOnlyList<string> GetConnectedCloudNodes()
    {
        return _nodes.Keys.ToList();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _runCts?.Cancel();

        // Disconnect all nodes
        foreach (var kvp in _nodes)
        {
            await kvp.Value.DisposeAsync();
        }
        _nodes.Clear();
        _deviceRegistry.Clear();

        _logger.LogInformation("Grid router stopped");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopAsync();
        _runCts?.Dispose();
    }

    private sealed class CloudNodeConnection(string nodeId, IStreamMultiplexer mux, DeltaTransit<GridMessage> transit) : IAsyncDisposable
    {
        public string NodeId => nodeId;
        public IStreamMultiplexer Mux => mux;
        public DeltaTransit<GridMessage> Transit => transit;

        public async ValueTask DisposeAsync()
        {
            await Transit.DisposeAsync();
            await Mux.DisposeAsync();
        }
    }
}
