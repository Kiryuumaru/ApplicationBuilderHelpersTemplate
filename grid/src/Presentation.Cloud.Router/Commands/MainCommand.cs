using Application.Cloud.Router.Grid.Interfaces.Inbound;
using Application.Cloud.Router.Grid.Models;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Infrastructure.NetConduit.Cloud.Router.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Presentation.Cli.Commands;

[Command("Main subcommand.")]
internal class MainCommand : Build.BaseCommand<WebApplicationBuilder>
{
    [CommandOption('p', "port", Description = "Port to listen on for cloud node connections.")]
    public int Port { get; set; } = 5000;

    protected override ValueTask<WebApplicationBuilder> ApplicationBuilder(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        return new ValueTask<WebApplicationBuilder>(builder);
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddGridRouter(new GridRouterOptions());
    }

    public override void AddMappings(ApplicationHost applicationHost, IHost host)
    {
        base.AddMappings(applicationHost, host);

        if (host is WebApplication app)
        {
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
        }
    }

    protected override async ValueTask Run(ApplicationHost<WebApplicationBuilder> applicationHost, CancellationTokenSource cancellationTokenSource)
    {
        await base.Run(applicationHost, cancellationTokenSource);

        var logger = applicationHost.Services.GetRequiredService<ILogger<MainCommand>>();
        var router = applicationHost.Services.GetRequiredService<IGridRouter>();

        router.DeviceConnected += deviceId =>
            logger.LogInformation("Device connected: {DeviceId}", deviceId);

        router.DeviceDisconnected += deviceId =>
            logger.LogInformation("Device disconnected: {DeviceId}", deviceId);

        router.CloudNodeConnected += nodeId =>
            logger.LogInformation("Cloud node connected: {NodeId}", nodeId);

        router.CloudNodeDisconnected += nodeId =>
            logger.LogInformation("Cloud node disconnected: {NodeId}", nodeId);

        router.DeviceMessageReceived += (deviceId, data) =>
            logger.LogDebug("Message from device {DeviceId}: {Length} bytes", deviceId, data.Length);

        await router.StartAsync(cancellationTokenSource.Token);

        logger.LogInformation("Grid Router started on port {Port}", Port);
        logger.LogInformation("Cloud nodes should connect to ws://localhost:{Port}/ws/node", Port);
    }
}
