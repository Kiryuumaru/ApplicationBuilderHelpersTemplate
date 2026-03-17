using Application.Cloud.Node.Grid.Interfaces.Inbound;
using Application.Cloud.Node.Grid.Models;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Infrastructure.NetConduit.Cloud.Node.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Presentation.Cli.Commands;

[Command("Main subcommand.")]
internal class MainCommand : Build.BaseCommand<WebApplicationBuilder>
{
    [CommandOption('p', "port", Description = "Port to listen on for device connections.")]
    public int Port { get; set; } = 8080;

    [CommandOption('r', "router", Description = "Router WebSocket endpoint to connect to.")]
    public string RouterEndpoint { get; set; } = "ws://localhost:5000/ws/node";

    protected override ValueTask<WebApplicationBuilder> ApplicationBuilder(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        return new ValueTask<WebApplicationBuilder>(builder);
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddGridCloudNode(new GridCloudOptions
        {
            RouterEndpoint = RouterEndpoint
        });
    }

    public override void AddMappings(ApplicationHost applicationHost, IHost host)
    {
        base.AddMappings(applicationHost, host);

        if (host is WebApplication app)
        {
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
        }
    }

    protected override async ValueTask Run(ApplicationHost<WebApplicationBuilder> applicationHost, CancellationTokenSource cancellationTokenSource)
    {
        await base.Run(applicationHost, cancellationTokenSource);

        var logger = applicationHost.Services.GetRequiredService<ILogger<MainCommand>>();
        var cloudNode = applicationHost.Services.GetRequiredService<IGridCloudNode>();

        cloudNode.DeviceConnected += deviceId =>
            logger.LogInformation("Device connected: {DeviceId}", deviceId);

        cloudNode.DeviceDisconnected += deviceId =>
            logger.LogInformation("Device disconnected: {DeviceId}", deviceId);

        cloudNode.DeviceMessageReceived += (deviceId, data) =>
            logger.LogDebug("Message from device {DeviceId}: {Length} bytes", deviceId, data.Length);

        await cloudNode.StartAsync(cancellationTokenSource.Token);

        logger.LogInformation("Cloud Node {NodeId} started on port {Port}", cloudNode.NodeId, Port);
        logger.LogInformation("Connected to router: {Connected}", cloudNode.IsConnectedToRouter);
        logger.LogInformation("Devices should connect to ws://localhost:{Port}/ws/device", Port);
    }
}
