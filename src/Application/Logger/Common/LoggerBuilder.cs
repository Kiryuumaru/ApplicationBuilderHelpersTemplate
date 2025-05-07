using Application.Configuration.Extensions;
using Application.Logger.Enrichers;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace Application.Logger.Common;

internal static class LoggerBuilder
{
    public static LoggerConfiguration Configure(LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        LogLevel logLevel = configuration.GetLoggerLevel();
        LogEventLevel logEventLevel = logLevel switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => throw new NotSupportedException(logLevel.ToString())
        };

        loggerConfiguration = loggerConfiguration
            .MinimumLevel.Is(logEventLevel)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.With(new LogGuidEnricher(configuration))
            .WriteTo.Console(new ExpressionTemplate(
                template: "[{@t:yyyy-MM-dd HH:mm:ss} {@l:u3}]{#if LogServiceName is not null} [{LogServiceName}{#if LogServiceActionName is not null}:{LogServiceActionName}{#end}]{#end} {@m} \n{@x}", theme: TemplateTheme.Code),
                restrictedToMinimumLevel: logEventLevel);

        string? useOtlpExporterEndpoint = configuration.GetRefValueOrDefault("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(useOtlpExporterEndpoint))
        {
            loggerConfiguration = loggerConfiguration
                .WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = useOtlpExporterEndpoint;
                    options.ResourceAttributes.Add("service.name", configuration.GetServiceName());
                });
        }

        var logsDumpDir = configuration.GetLogsDumpDirectory();
        if (logsDumpDir != null)
        {
            loggerConfiguration = loggerConfiguration
                .MinimumLevel.Verbose()
                .WriteTo.File(
                    formatter: new CompactJsonFormatter(),
                    path: logsDumpDir / "log-.jsonl",
                    restrictedToMinimumLevel: LogEventLevel.Verbose,
                    rollingInterval: RollingInterval.Hour);
        }

        var defaultLogsDump = configuration.GetLogsPath() / configuration.GetServiceName().ToLowerInvariant();
        loggerConfiguration = loggerConfiguration
            .MinimumLevel.Verbose()
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: defaultLogsDump / "log-.jsonl",
                restrictedToMinimumLevel: LogEventLevel.Verbose,
                rollingInterval: RollingInterval.Hour);

        return loggerConfiguration;
    }
}
