using AbsolutePathHelpers;
using Application.Configuration.Extensions;
using Application.Configuration.Interfaces;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Presentation.Commands;

public abstract class BaseCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] THostApplicationBuilder> : ApplicationCommand<THostApplicationBuilder>
    where THostApplicationBuilder : IHostApplicationBuilder
{
    public IApplicationConstants ApplicationConstants { get; } = new ApplicationConstants();

    [CommandOption(
        'l', "log-level",
        EnvironmentVariable = "LOG_LEVEL",
        Description = "Level of logs to show.")]
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    [CommandOption(
        "logs-dump",
        EnvironmentVariable = "LOGS_DUMP",
        Description = "Logs dump to directory.")]
    public AbsolutePath? LogsDumpDirectory { get; set; }

    public override bool ExitOnRunComplete => true;

    protected BaseCommand(string? description = null)
        : base(description)
    {
    }

    protected BaseCommand(string name, string? description = null)
        : base(name, description)
    {
    }

    public override void AddConfigurations(ApplicationHostBuilder applicationBuilder, IConfiguration configuration)
    {
        base.AddConfigurations(applicationBuilder, configuration);

        applicationBuilder.Services.Configure<ConsoleLifetimeOptions>(opts => opts.SuppressStatusMessages = true);

        configuration.SetLoggerLevel(LogLevel);

        configuration.SetLogsDumpDirectory(LogsDumpDirectory);

        configuration.SetServiceName(ApplicationConstants.AppName);
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        services.AddSingleton(ApplicationConstants);

        base.AddServices(applicationBuilder, services);
    }

    protected string BuildAppBanner()
    {
        return ApplicationConstants.BuildAppBanner(Build.Constants.FullVersion);
    }
}

internal class ApplicationConstants : IApplicationConstants
{
    public string AppName { get; } = Build.Constants.AppName;

    public string AppTitle { get; } = Build.Constants.AppTitle;

    public string AppTag { get; } = Build.Constants.AppTag;
}
