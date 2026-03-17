using System.Net.WebSockets;
using System.Text;
using Application.Cloud.Node.Grid.Interfaces.Inbound;
using Application.Cloud.Node.Grid.Models;
using Application.Cloud.Router.Grid.Interfaces.Inbound;
using Application.Cloud.Router.Grid.Models;
using Application.Edge.Node.Grid.Interfaces.Inbound;
using Application.Edge.Node.Grid.Models;
using Infrastructure.NetConduit.Cloud.Node.Extensions;
using Infrastructure.NetConduit.Cloud.Router.Extensions;
using Infrastructure.NetConduit.Edge.Node.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Grid.IntegrationTests;

/// <summary>
/// End-to-end integration tests for the Grid system.
/// Tests Router → Cloud Node → Edge Device connectivity and message flow.
/// </summary>
public sealed class GridEndToEndTests : IAsyncLifetime
{
    // Use port 0 to let OS assign available ports
    private int _routerPort;
    private int _cloudNodePort;

    private readonly ITestOutputHelper _output;
    private WebApplication? _routerApp;
    private WebApplication? _cloudNodeApp;
    private IGridRouter? _router;
    private IGridCloudNode? _cloudNode;

    public GridEndToEndTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("=== Starting Grid Integration Test Infrastructure ===");
        _output.WriteLine("");

        // Start Router with dynamic port
        _output.WriteLine("[SETUP] Starting Grid Router...");
        _routerApp = await StartRouterAsync();
        _routerPort = GetAssignedPort(_routerApp);
        _router = _routerApp.Services.GetRequiredService<IGridRouter>();
        await _router.StartAsync();
        _output.WriteLine("[SETUP] Router started on port {0}", _routerPort);
        _output.WriteLine("");

        // Start Cloud Node (connects to router)
        _output.WriteLine("[SETUP] Starting Cloud Node, connecting to router...");
        _cloudNodeApp = await StartCloudNodeAsync();
        _cloudNodePort = GetAssignedPort(_cloudNodeApp);
        _cloudNode = _cloudNodeApp.Services.GetRequiredService<IGridCloudNode>();
        await _cloudNode.StartAsync();

        // Wait for cloud node to connect to router
        await WaitForConditionAsync(() => _cloudNode.IsConnectedToRouter, TimeSpan.FromSeconds(10));
        _output.WriteLine("[SETUP] Cloud Node {0} connected to router on port {1}", _cloudNode.NodeId, _cloudNodePort);
        _output.WriteLine("");
        _output.WriteLine("=== Infrastructure Ready ===");
        _output.WriteLine("");
    }

    private static int GetAssignedPort(WebApplication app)
    {
        var address = app.Urls.FirstOrDefault() ?? throw new InvalidOperationException("No address bound");
        var uri = new Uri(address);
        return uri.Port;
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine("");
        _output.WriteLine("=== Shutting Down Grid Infrastructure ===");

        if (_cloudNode is not null)
        {
            await _cloudNode.StopAsync();
            _output.WriteLine("[TEARDOWN] Cloud Node stopped");
        }

        if (_router is not null)
        {
            await _router.StopAsync();
            _output.WriteLine("[TEARDOWN] Router stopped");
        }

        if (_cloudNodeApp is not null)
        {
            await _cloudNodeApp.StopAsync();
            await _cloudNodeApp.DisposeAsync();
        }

        if (_routerApp is not null)
        {
            await _routerApp.StopAsync();
            await _routerApp.DisposeAsync();
        }

        _output.WriteLine("[TEARDOWN] All components disposed");
        _output.WriteLine("=== Test Completed ===");
    }

    [Fact]
    public async Task Device_Can_Connect_And_Send_Messages_Through_Grid()
    {
        _output.WriteLine("--- TEST: Device_Can_Connect_And_Send_Messages_Through_Grid ---");
        _output.WriteLine("");

        // Create device node
        var deviceId = "device-test-001";
        _output.WriteLine("[TEST] Creating device node with ID: {0}", deviceId);

        var deviceServices = new ServiceCollection();
        deviceServices.AddLogging(builder => builder.AddProvider(new XunitLoggerProvider(_output, $"[Device {deviceId}]")));
        deviceServices.AddGridDeviceNode(new GridDeviceOptions
        {
            DeviceId = deviceId,
            CloudNodeEndpoint = $"ws://localhost:{_cloudNodePort}/ws/device"
        });

        await using var deviceProvider = deviceServices.BuildServiceProvider();
        var device = deviceProvider.GetRequiredService<IGridDeviceNode>();

        // Track events
        var connectedTcs = new TaskCompletionSource();
        var messageReceivedTcs = new TaskCompletionSource<byte[]>();

        device.Connected += () =>
        {
            _output.WriteLine("[EVENT] Device connected event fired");
            connectedTcs.TrySetResult();
        };

        device.MessageReceived += data =>
        {
            _output.WriteLine("[EVENT] Device received message: {0} bytes", data.Length);
            messageReceivedTcs.TrySetResult(data.ToArray());
        };

        // Connect device
        _output.WriteLine("[TEST] Connecting device to cloud node...");
        await device.ConnectAsync();

        // Wait for connection
        await connectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(device.IsConnected, "Device should be connected");
        _output.WriteLine("[TEST] Device connected: {0}", device.IsConnected);

        // Wait for router to register the device
        await WaitForConditionAsync(() => _router!.IsDeviceConnected(deviceId), TimeSpan.FromSeconds(5));
        _output.WriteLine("[TEST] Router sees device connected: {0}", _router!.IsDeviceConnected(deviceId));

        // Send message from device
        var testMessage = Encoding.UTF8.GetBytes("Hello from device!");
        _output.WriteLine("[TEST] Device sending message: '{0}'", Encoding.UTF8.GetString(testMessage));
        await device.SendAsync(testMessage);

        // Small delay to let message propagate
        await Task.Delay(500);

        // Send message to device from router
        var responseMessage = Encoding.UTF8.GetBytes("Hello from router!");
        _output.WriteLine("[TEST] Router sending message to device: '{0}'", Encoding.UTF8.GetString(responseMessage));
        await _router.SendToDeviceAsync(deviceId, responseMessage);

        // Wait for device to receive message
        var receivedData = await messageReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var receivedMessage = Encoding.UTF8.GetString(receivedData);
        _output.WriteLine("[TEST] Device received: '{0}'", receivedMessage);

        Assert.Equal("Hello from router!", receivedMessage);

        // Disconnect device
        _output.WriteLine("[TEST] Disconnecting device...");
        await device.DisconnectAsync();

        // Wait for router to see disconnection
        await WaitForConditionAsync(() => !_router.IsDeviceConnected(deviceId), TimeSpan.FromSeconds(5));
        _output.WriteLine("[TEST] Device disconnected from grid");

        _output.WriteLine("");
        _output.WriteLine("--- TEST PASSED ---");
    }

    [Fact]
    public async Task Multiple_Devices_Can_Connect_Simultaneously()
    {
        _output.WriteLine("--- TEST: Multiple_Devices_Can_Connect_Simultaneously ---");
        _output.WriteLine("");

        var deviceCount = 3;
        var devices = new List<IGridDeviceNode>();
        var providers = new List<ServiceProvider>();

        try
        {
            // Create and connect multiple devices
            for (int i = 0; i < deviceCount; i++)
            {
                var deviceId = $"multi-device-{i:D3}";
                _output.WriteLine("[TEST] Creating and connecting device: {0}", deviceId);

                var deviceServices = new ServiceCollection();
                deviceServices.AddLogging(builder => builder.AddProvider(new XunitLoggerProvider(_output, $"[Device {deviceId}]")));
                deviceServices.AddGridDeviceNode(new GridDeviceOptions
                {
                    DeviceId = deviceId,
                    CloudNodeEndpoint = $"ws://localhost:{_cloudNodePort}/ws/device"
                });

                var provider = deviceServices.BuildServiceProvider();
                providers.Add(provider);

                var device = provider.GetRequiredService<IGridDeviceNode>();
                devices.Add(device);

                await device.ConnectAsync();
                await WaitForConditionAsync(() => device.IsConnected, TimeSpan.FromSeconds(5));
                _output.WriteLine("[TEST] Device {0} connected: {1}", deviceId, device.IsConnected);
            }

            // Wait for all devices to be registered with router
            await WaitForConditionAsync(() => _router!.GetConnectedDeviceIds().Count == deviceCount, TimeSpan.FromSeconds(5));

            // Verify all devices are registered with router
            _output.WriteLine("");
            _output.WriteLine("[TEST] Verifying all devices registered with router...");
            var connectedDevices = _router!.GetConnectedDeviceIds();
            _output.WriteLine("[TEST] Router sees {0} connected devices: {1}",
                connectedDevices.Count, string.Join(", ", connectedDevices));

            Assert.Equal(deviceCount, connectedDevices.Count);

            // Verify cloud node device count
            _output.WriteLine("[TEST] Cloud node reports {0} connected devices", _cloudNode!.ConnectedDeviceCount);
            Assert.Equal(deviceCount, _cloudNode.ConnectedDeviceCount);

            _output.WriteLine("");
            _output.WriteLine("--- TEST PASSED ---");
        }
        finally
        {
            // Cleanup
            _output.WriteLine("");
            _output.WriteLine("[CLEANUP] Disconnecting all devices...");
            foreach (var device in devices)
            {
                await device.DisconnectAsync();
            }
            foreach (var provider in providers)
            {
                await provider.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Broadcast_Message_Reaches_All_Devices()
    {
        _output.WriteLine("--- TEST: Broadcast_Message_Reaches_All_Devices ---");
        _output.WriteLine("");

        var deviceCount = 2;
        var devices = new List<IGridDeviceNode>();
        var providers = new List<ServiceProvider>();
        var messageReceivedTcs = new List<TaskCompletionSource<byte[]>>();

        try
        {
            // Create and connect devices
            for (int i = 0; i < deviceCount; i++)
            {
                var deviceId = $"broadcast-device-{i:D3}";
                _output.WriteLine("[TEST] Creating device: {0}", deviceId);

                var tcs = new TaskCompletionSource<byte[]>();
                messageReceivedTcs.Add(tcs);

                var deviceServices = new ServiceCollection();
                deviceServices.AddLogging(builder => builder.AddProvider(new XunitLoggerProvider(_output, $"[Device {deviceId}]")));
                deviceServices.AddGridDeviceNode(new GridDeviceOptions
                {
                    DeviceId = deviceId,
                    CloudNodeEndpoint = $"ws://localhost:{_cloudNodePort}/ws/device"
                });

                var provider = deviceServices.BuildServiceProvider();
                providers.Add(provider);

                var device = provider.GetRequiredService<IGridDeviceNode>();
                devices.Add(device);

                var capturedTcs = tcs;
                device.MessageReceived += data =>
                {
                    _output.WriteLine("[EVENT] Device {0} received broadcast: {1} bytes", deviceId, data.Length);
                    capturedTcs.TrySetResult(data.ToArray());
                };

                await device.ConnectAsync();
                await WaitForConditionAsync(() => device.IsConnected, TimeSpan.FromSeconds(5));
            }

            // Wait for all devices to be registered
            await WaitForConditionAsync(() => _router!.GetConnectedDeviceIds().Count == deviceCount, TimeSpan.FromSeconds(5));
            _output.WriteLine("[TEST] All {0} devices connected and registered", deviceCount);

            // Broadcast message
            var broadcastMessage = Encoding.UTF8.GetBytes("Broadcast to all!");
            _output.WriteLine("[TEST] Broadcasting message: '{0}'", Encoding.UTF8.GetString(broadcastMessage));
            await _router!.BroadcastToAllDevicesAsync(broadcastMessage);

            // Wait for all devices to receive
            _output.WriteLine("[TEST] Waiting for all devices to receive broadcast...");
            var timeout = TimeSpan.FromSeconds(10);
            var receiveTasks = messageReceivedTcs.Select(tcs => tcs.Task.WaitAsync(timeout)).ToArray();
            var results = await Task.WhenAll(receiveTasks);

            // Verify all received the same message
            foreach (var result in results)
            {
                var received = Encoding.UTF8.GetString(result);
                _output.WriteLine("[TEST] Device received: '{0}'", received);
                Assert.Equal("Broadcast to all!", received);
            }

            _output.WriteLine("");
            _output.WriteLine("--- TEST PASSED ---");
        }
        finally
        {
            _output.WriteLine("");
            _output.WriteLine("[CLEANUP] Disconnecting all devices...");
            foreach (var device in devices)
            {
                await device.DisconnectAsync();
            }
            foreach (var provider in providers)
            {
                await provider.DisposeAsync();
            }
        }
    }

    private async Task<WebApplication> StartRouterAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new XunitLoggerProvider(_output, "[Router]"));

        builder.Services.AddGridRouter(new GridRouterOptions());

        var app = builder.Build();
        app.UseWebSockets();

        app.Map("/ws/node", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var router = context.RequestServices.GetRequiredService<IGridRouter>();
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await router.HandleNodeConnectionAsync(webSocket, context.RequestAborted);
        });

        await app.StartAsync();
        return app;
    }

    private async Task<WebApplication> StartCloudNodeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new XunitLoggerProvider(_output, "[CloudNode]"));

        builder.Services.AddGridCloudNode(new GridCloudOptions
        {
            RouterEndpoint = $"ws://127.0.0.1:{_routerPort}/ws/node"
        });

        var app = builder.Build();
        app.UseWebSockets();

        app.Map("/ws/device", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var cloudNode = context.RequestServices.GetRequiredService<IGridCloudNode>();
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await cloudNode.HandleDeviceConnectionAsync(webSocket, context.RequestAborted);
        });

        await app.StartAsync();
        return app;
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        if (!condition())
        {
            throw new TimeoutException($"Condition not met within {timeout}");
        }
    }
}

/// <summary>
/// Logger provider that outputs to xUnit test output.
/// </summary>
internal sealed class XunitLoggerProvider(ITestOutputHelper output, string prefix) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        // Simplify category name
        var shortName = categoryName.Split('.').LastOrDefault() ?? categoryName;
        return new XunitLogger(output, prefix, shortName);
    }

    public void Dispose() { }
}

/// <summary>
/// Logger that outputs to xUnit test output.
/// </summary>
internal sealed class XunitLogger(ITestOutputHelper output, string prefix, string categoryName) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var levelShort = logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };

        var message = formatter(state, exception);
        try
        {
            output.WriteLine("{0} {1} [{2}] {3}", prefix, levelShort, categoryName, message);
            if (exception is not null)
            {
                output.WriteLine("{0} {1} [{2}] Exception: {3}", prefix, levelShort, categoryName, exception);
            }
        }
        catch (InvalidOperationException)
        {
            // Test has ended, ignore
        }
    }
}
