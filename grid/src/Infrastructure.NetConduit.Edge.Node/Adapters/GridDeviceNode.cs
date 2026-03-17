using Application.Edge.Node.Grid.Interfaces.Inbound;
using Application.Edge.Node.Grid.Models;
using Domain.Grid.Enums;
using Domain.Grid.Models;
using Infrastructure.NetConduit.Serialization;
using Microsoft.Extensions.Logging;
using NetConduit;
using NetConduit.Transits;
using NetConduit.WebSocket;

namespace Infrastructure.NetConduit.Edge.Node.Adapters;

/// <summary>
/// Grid device node implementation using NetConduit WebSocket with DeltaTransit.
/// </summary>
internal sealed class GridDeviceNode : IGridDeviceNode
{
    private readonly GridDeviceOptions _options;
    private readonly ILogger<GridDeviceNode> _logger;
    private IStreamMultiplexer? _mux;
    private DeltaTransit<GridMessage>? _controlTransit;
    private CancellationTokenSource? _runCts;
    private Task? _receiveTask;
    private volatile bool _isConnected;
    private bool _disposed;

    public GridDeviceNode(GridDeviceOptions options, ILogger<GridDeviceNode> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string DeviceId => _options.DeviceId;
    public bool IsConnected => _isConnected;

    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<ReadOnlyMemory<byte>>? MessageReceived;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GridDeviceNode));

        _logger.LogInformation("Connecting device {DeviceId} to {Endpoint}", DeviceId, _options.CloudNodeEndpoint);

        // Create options with defaults - EnableReconnection is true by default
        var muxOptions = WebSocketMultiplexer.CreateOptions(_options.CloudNodeEndpoint);

        _mux = StreamMultiplexer.Create(muxOptions);

        _mux.OnDisconnected += (reason, ex) =>
        {
            _isConnected = false;
            _logger.LogWarning("Device {DeviceId} disconnected: {Reason}", DeviceId, reason);
            Disconnected?.Invoke();
        };

        // Note: OnReconnected event handled via monitoring state changes
        // Reconnection is automatic when EnableReconnection is true (default)

        _ = _mux.Start();
        await _mux.WaitForReadyAsync(cancellationToken);

        // Open control channel with DeltaTransit for efficient delta messaging
        _controlTransit = await _mux.OpenDeltaTransitAsync(
            "control",
            NetConduitJsonContext.Default.GridMessage,
            cancellationToken: cancellationToken);

        // Register with cloud node
        await RegisterWithNodeAsync(cancellationToken);

        // Start receive loop
        _runCts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_runCts.Token);
    }

    private async Task RegisterWithNodeAsync(CancellationToken cancellationToken)
    {
        if (_controlTransit is null)
            return;

        var registerMsg = GridMessage.CreateDeviceRegister(DeviceId);
        await _controlTransit.SendAsync(registerMsg, cancellationToken);

        var response = await _controlTransit.ReceiveAsync(cancellationToken);
        if (response is null)
        {
            _logger.LogError("No response to device registration");
            return;
        }

        if (response.Type == GridMessageType.DeviceRegisterAck && response.ErrorCode == GridErrorCode.None)
        {
            _isConnected = true;
            _logger.LogInformation("Device {DeviceId} registered successfully", DeviceId);
            Connected?.Invoke();
        }
        else
        {
            _logger.LogError("Device registration failed: {ErrorCode}", response.ErrorCode);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_controlTransit is null)
            return;

        try
        {
            await foreach (var message in _controlTransit.ReceiveAllAsync(cancellationToken))
            {
                HandleMessage(message);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in receive loop for device {DeviceId}", DeviceId);
        }
    }

    private void HandleMessage(GridMessage message)
    {
        switch (message.Type)
        {
            case GridMessageType.Data:
            case GridMessageType.SendToDevice:
                if (message.Payload is not null)
                {
                    MessageReceived?.Invoke(message.Payload);
                }
                break;

            case GridMessageType.Ping:
                _ = Task.Run(async () =>
                {
                    if (_controlTransit is not null)
                        await _controlTransit.SendAsync(GridMessage.CreatePong());
                });
                break;

            default:
                _logger.LogDebug("Received message type {Type}", message.Type);
                break;
        }
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (_controlTransit is null || !_isConnected)
            throw new InvalidOperationException("Not connected to cloud node");

        var message = GridMessage.CreateData(DeviceId, null, data.ToArray());
        await _controlTransit.SendAsync(message, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _runCts?.Cancel();

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Receive task did not complete in time");
            }
        }

        if (_controlTransit is not null)
        {
            await _controlTransit.DisposeAsync();
            _controlTransit = null;
        }

        if (_mux is not null)
        {
            await _mux.DisposeAsync();
            _mux = null;
        }

        _isConnected = false;
        _logger.LogInformation("Device {DeviceId} disconnected", DeviceId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await DisconnectAsync();
        _runCts?.Dispose();
    }
}
