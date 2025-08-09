using Application.Common.Configuration.Extensions;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Extensions;
using Infrastructure.Serilog.Logger.Enrichers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Json;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace Infrastructure.Serilog.Logger.Extensions;

public static class LoggerApplicationHostBuilderExtensions
{
    static readonly TemplateTheme ConsoleTheme = new(new Dictionary<TemplateThemeStyle, string>
    {
        [TemplateThemeStyle.Text] = "\u001b[38;5;0253m",
        [TemplateThemeStyle.SecondaryText] = "\u001b[38;5;0246m",
        [TemplateThemeStyle.TertiaryText] = "\u001b[38;5;0242m",
        [TemplateThemeStyle.Invalid] = "\u001b[33;1m",
        [TemplateThemeStyle.Null] = "\u001b[38;5;0038m",
        [TemplateThemeStyle.Name] = "\u001b[38;5;0081m",
        [TemplateThemeStyle.String] = "\u001b[38;5;0216m",
        [TemplateThemeStyle.Number] = "\u001b[38;5;151m",
        [TemplateThemeStyle.Boolean] = "\u001b[38;5;0038m",
        [TemplateThemeStyle.Scalar] = "\u001b[38;5;0079m",
        [TemplateThemeStyle.LevelVerbose] = "\u001b[38;5;0242m",
        [TemplateThemeStyle.LevelDebug] = "\u001b[38;5;0246m",
        [TemplateThemeStyle.LevelInformation] = "\u001b[37;1m",
        [TemplateThemeStyle.LevelWarning] = "\u001b[38;5;0229m",
        [TemplateThemeStyle.LevelError] = "\u001b[38;5;0197m\u001b[48;5;0238m",
        [TemplateThemeStyle.LevelFatal] = "\u001b[37;1m\u001b[48;5;197m"
    });

    public static ApplicationHostBuilder AddLoggerConfiguration(this ApplicationHostBuilder applicationBuilder, Dictionary<string, object?>? scopeMap = null)
    {
        var configuredLogger = Configure(new LoggerConfiguration(), applicationBuilder, scopeMap).CreateLogger();

        Log.Logger = configuredLogger;

        applicationBuilder.Builder.Logging.ClearProviders();
        applicationBuilder.Builder.Logging.AddSerilog(dispose: true);

        applicationBuilder.Builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        applicationBuilder.Builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(applicationBuilder.Builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        return applicationBuilder;
    }

    public static ApplicationHostBuilder AddLoggerServices(this ApplicationHostBuilder applicationBuilder)
    {
        if (applicationBuilder.Configuration.ContainsRefValue("OTEL_EXPORTER_OTLP_ENDPOINT"))
        {
            applicationBuilder.Builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return applicationBuilder;
    }

    public static void AddLoggerMiddlewares(this ApplicationHost applicationHost)
    {
        if (applicationHost.Host is WebApplication webApplicationBuilder)
        {
            webApplicationBuilder.UseSerilogRequestLogging();
        }
    }

    private static LoggerConfiguration Configure(LoggerConfiguration loggerConfiguration, ApplicationHostBuilder applicationBuilder, Dictionary<string, object?>? scopeMap)
    {
        var hostApplicationBuilder = applicationBuilder.Builder;

        LogLevel logLevel = hostApplicationBuilder.Configuration.GetLoggerLevel();

        loggerConfiguration = loggerConfiguration
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ApplicationName", hostApplicationBuilder.Environment.ApplicationName)
            .Enrich.WithProperty("Environment", hostApplicationBuilder.Environment.EnvironmentName)
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.With(new LogGuidEnricher(hostApplicationBuilder.Configuration, scopeMap));

        if (logLevel != LogLevel.None)
        {
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
                .MinimumLevel.Is(logEventLevel);

            if (logEventLevel == LogEventLevel.Information)
            {
                loggerConfiguration = loggerConfiguration
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                    .MinimumLevel.Override("Polly", LogEventLevel.Warning);
            }
            else
            {
                loggerConfiguration = loggerConfiguration
                    .MinimumLevel.Override("Microsoft", logEventLevel)
                    .MinimumLevel.Override("Microsoft.AspNetCore", logEventLevel)
                    .MinimumLevel.Override("System.Net.Http.HttpClient", logEventLevel)
                    .MinimumLevel.Override("Polly", logEventLevel);
            }

            loggerConfiguration = loggerConfiguration
                .WriteTo.Console(new ExpressionTemplate(
                    template: "[{@t:yyyy-MM-dd HH:mm:ss} {@l:u3}]{#if SourceContext is not null} [{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}{#if SourceContextAction is not null}:{SourceContextAction}{#end}]{#end} {@m} \n{@x}",
                    theme: ConsoleTheme),
                    restrictedToMinimumLevel: logEventLevel);
        }

        var logsDumpDir = hostApplicationBuilder.Configuration.GetLogsDumpDirectory();
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

        return loggerConfiguration;
    }
}