using Application.Edge.Node.Grid.Interfaces.Inbound;
using Application.Edge.Node.Grid.Models;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Infrastructure.NetConduit.Edge.Node.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Presentation.Cli.Commands;

[Command("Main subcommand.")]
internal class MainCommand : Build.BaseCommand<HostApplicationBuilder>
{
    [CommandOption('d', "device-id", Description = "Unique device identifier.")]
    public string DeviceId { get; set; } = $"device-{Guid.NewGuid().ToString("N")[..8]}";

    [CommandOption('c', "cloud-node", Description = "Cloud node WebSocket endpoint to connect to.")]
    public string CloudNodeEndpoint { get; set; } = "ws://localhost:8080/ws/device";

    protected override ValueTask<HostApplicationBuilder> ApplicationBuilder(CancellationToken stoppingToken)
    {
        var builder = Host.CreateApplicationBuilder();
        return new ValueTask<HostApplicationBuilder>(builder);
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddGridDeviceNode(new GridDeviceOptions
        {
            DeviceId = DeviceId,
            CloudNodeEndpoint = CloudNodeEndpoint
        });
    }

    protected override async ValueTask Run(ApplicationHost<HostApplicationBuilder> applicationHost, CancellationTokenSource cancellationTokenSource)
    {
        await base.Run(applicationHost, cancellationTokenSource);

        var logger = applicationHost.Services.GetRequiredService<ILogger<MainCommand>>();
        var deviceNode = applicationHost.Services.GetRequiredService<IGridDeviceNode>();

        deviceNode.Connected += () =>
            logger.LogInformation("Connected to cloud node");

        deviceNode.Disconnected += () =>
            logger.LogWarning("Disconnected from cloud node");

        deviceNode.MessageReceived += data =>
        {
            var text = Encoding.UTF8.GetString(data.Span);
            logger.LogInformation("Received message: {Message}", text);
        };

        logger.LogInformation("Edge Node {DeviceId} connecting to {Endpoint}", DeviceId, CloudNodeEndpoint);

        try
        {
            await deviceNode.ConnectAsync(cancellationTokenSource.Token);
            logger.LogInformation("Edge Node {DeviceId} connected successfully", DeviceId);

            // Keep running and send periodic heartbeats
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationTokenSource.Token);

                if (deviceNode.IsConnected)
                {
                    var heartbeat = Encoding.UTF8.GetBytes($"heartbeat from {DeviceId} at {DateTimeOffset.UtcNow}");
                    await deviceNode.SendAsync(heartbeat, cancellationTokenSource.Token);
                    logger.LogDebug("Sent heartbeat");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in edge node");
        }
        finally
        {
            await deviceNode.DisconnectAsync();
        }
    }
}
