using Application.Configuration.Interfaces;
using Application.Logger.Extensions;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Presentation.Commands;

public class MainCommand : BaseCommand<HostApplicationBuilder>
{
    public MainCommand() : base("Main subcommand.")
    {
    }

    protected override ValueTask<HostApplicationBuilder> ApplicationBuilder(CancellationToken stoppingToken)
    {
        return new ValueTask<HostApplicationBuilder>(Host.CreateApplicationBuilder());
    }

    protected override async ValueTask Run(ApplicationHost<HostApplicationBuilder> applicationHost, CancellationToken stoppingToken)
    {
        await base.Run(applicationHost, stoppingToken);

        var logger = applicationHost.Services.GetRequiredService<ILogger<MainCommand>>();
        
        using var _ = logger.BeginScopeMap<MainCommand>(scopeMap: new Dictionary<string, object?>
        {
            { "AppName", ApplicationConstants.AppName },
            { "AppTitle", ApplicationConstants.AppTitle },
            { "AppTag", ApplicationConstants.AppTag }
        });

        logger.LogTrace("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogDebug("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogInformation("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogWarning("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogError("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogCritical("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);

        logger.LogInformation("Boolean (true): {Value}", true);
        logger.LogInformation("Boolean (false): {Value}", false);
        logger.LogInformation("Integer: {Value}", 123);
        logger.LogInformation("Float: {Value}", 123.45f);
        logger.LogInformation("Double: {Value}", 3.14159);
        logger.LogInformation("Decimal: {Value}", 99.99m);
        logger.LogInformation("String: {Value}", "Hello, world!");
        logger.LogInformation("Null: {Value}", null!);
        logger.LogInformation("DateTime: {Value}", DateTime.Now);
        logger.LogInformation("Guid: {Value}", Guid.NewGuid());
        logger.LogInformation("Object: {@Value}", new { Name = "Test", Age = 42 });
        logger.LogInformation("Array: {@Value}", new[] { 1, 2, 3 });
        logger.LogInformation("Enum: {Value}", ConsoleColor.Red);
    }
}
