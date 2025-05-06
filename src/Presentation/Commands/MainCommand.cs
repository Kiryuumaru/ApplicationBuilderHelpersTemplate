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

        logger.LogInformation("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
    }
}
