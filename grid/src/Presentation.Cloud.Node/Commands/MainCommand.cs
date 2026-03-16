using Application.HelloWorld.Interfaces.Inbound;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Presentation.Cli.Commands;

[Command("Main subcommand.")]
internal class MainCommand : Build.BaseCommand<HostApplicationBuilder>
{
    [CommandOption('m', "message", Description = "The hello world message to use.")]
    public string Message { get; set; } = "Hello, World!";

    protected override ValueTask<HostApplicationBuilder> ApplicationBuilder(CancellationToken stoppingToken)
    {
        var builder = Host.CreateApplicationBuilder();
        return new ValueTask<HostApplicationBuilder>(builder);
    }

    protected override async ValueTask Run(ApplicationHost<HostApplicationBuilder> applicationHost, CancellationTokenSource cancellationTokenSource)
    {
        await base.Run(applicationHost, cancellationTokenSource);

        using var scope = applicationHost.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MainCommand>>();

        // Presentation calls Application service (Interfaces/Inbound) - no direct entity manipulation
        var helloWorldService = scope.ServiceProvider.GetRequiredService<IHelloWorldService>();

        logger.LogInformation("Calling HelloWorld service with message: {Message}", Message);
        logger.LogInformation("---");

        // Single call to Application service triggers:
        // 1. Entity creation (raises domain event)
        // 2. Event dispatch to all handlers (in parallel)
        // 3. Each handler executes independently (decoupled side effects)
        var result = await helloWorldService.CreateGreetingAsync(Message, cancellationTokenSource.Token);

        logger.LogInformation("---");
        logger.LogInformation("Result: EntityId={EntityId}, Message=\"{Message}\", CreatedAt={CreatedAt}",
            result.EntityId,
            result.Message,
            result.CreatedAt);

        cancellationTokenSource.Cancel();
    }
}
