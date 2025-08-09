using AbsolutePathHelpers;
using Application.Abstractions.Application;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Application.Common.Configuration.Extensions;

namespace Presentation.Commands;

public abstract class BaseCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] THostApplicationBuilder> : Command<THostApplicationBuilder>
    where THostApplicationBuilder : IHostApplicationBuilder
{
    public IApplicationConstants ApplicationConstants { get; } = Build.ApplicationConstants.Instance;

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
}
