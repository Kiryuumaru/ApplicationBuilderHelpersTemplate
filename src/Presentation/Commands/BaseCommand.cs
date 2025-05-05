using AbsolutePathHelpers;
using Application.Configuration.Extensions;
using Application.Configuration.Interfaces;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Presentation.Commands;

public abstract class BaseCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] THostApplicationBuilder> : ApplicationCommand<THostApplicationBuilder>
    where THostApplicationBuilder : IHostApplicationBuilder
{
    public abstract IApplicationConstants ApplicationConstants { get; }

    [CommandOption(
        'l', "log-level",
        EnvironmentVariable = "LOG_LEVEL",
        Description = "Level of logs to show.",
        FromAmong = ["Trace", "Debug", "Information", "Warning", "Error", "Critical"],
        CaseSensitive = false)]
    public string LogLevel { get; set; } = "Information";

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

    public override void AddConfiguration(ApplicationHostBuilder applicationBuilder, IConfiguration configuration)
    {
        base.AddConfiguration(applicationBuilder, configuration);

        applicationBuilder.Services.Configure<ConsoleLifetimeOptions>(opts => opts.SuppressStatusMessages = true);

        configuration.SetLoggerLevel(LogLevel.ToLowerInvariant() switch
        {
            "trace" => Microsoft.Extensions.Logging.LogLevel.Trace,
            "debug" => Microsoft.Extensions.Logging.LogLevel.Debug,
            "information" => Microsoft.Extensions.Logging.LogLevel.Information,
            "warning" => Microsoft.Extensions.Logging.LogLevel.Warning,
            "error" => Microsoft.Extensions.Logging.LogLevel.Error,
            "critical" => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => throw new NotImplementedException($"{LogLevel} is invalid log level.")
        });

        configuration.SetLogsDumpDirectory(LogsDumpDirectory);

        configuration.SetServiceName(ApplicationConstants.AppNameSnakeCase);
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
