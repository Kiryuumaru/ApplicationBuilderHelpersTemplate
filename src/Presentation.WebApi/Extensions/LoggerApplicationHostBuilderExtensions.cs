using Application.Configuration.Extensions;
using Application.Logger.Enrichers;
using Application.Logger.Interfaces;
using Application.Logger.Services;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace Presentation.WebApi.Extensions;

internal static class LoggerApplicationHostBuilderExtensions
{
    private static readonly TemplateTheme ConsoleTheme = new(new Dictionary<TemplateThemeStyle, string>
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
        scopeMap ??= [];

        var configuredLogger = Configure(new LoggerConfiguration(), applicationBuilder, scopeMap).CreateLogger();

        Log.Logger = configuredLogger;

        applicationBuilder.Builder.Logging.ClearProviders();

        if (applicationBuilder.Builder is WebApplicationBuilder webApplicationBuilder)
        {
            webApplicationBuilder.Host.UseSerilog(configuredLogger, dispose: true);
        }
        else
        {
            applicationBuilder.Builder.Logging.AddSerilog(configuredLogger, dispose: true);
        }

        applicationBuilder.Builder.Logging.SetMinimumLevel(applicationBuilder.Configuration.GetLoggerLevel());

        applicationBuilder.Builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        return applicationBuilder;
    }

    public static ApplicationHostBuilder AddLoggerServices(this ApplicationHostBuilder applicationBuilder)
    {
        applicationBuilder.Builder.Services
            .AddOpenTelemetry()
            .WithLogging(logging =>
            {
                if (applicationBuilder.Builder is WebApplicationBuilder &&
                    applicationBuilder.Configuration.ContainsRefValue("OTEL_EXPORTER_OTLP_ENDPOINT"))
                {
                    logging.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                if (applicationBuilder.Configuration.ContainsRefValue("OTEL_EXPORTER_OTLP_ENDPOINT"))
                {
                    metrics.AddOtlpExporter();
                }

                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Application.Edge.ServiceWrapper");

            })
            .WithTracing(tracing =>
            {
                if (applicationBuilder.Configuration.ContainsRefValue("OTEL_EXPORTER_OTLP_ENDPOINT"))
                {
                    tracing.AddOtlpExporter();
                }

                if (applicationBuilder.Builder is WebApplicationBuilder)
                {
                    tracing.AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments("/health") &&
                            !context.Request.Path.StartsWithSegments("/alive");
                    });
                }

                tracing
                    .AddHttpClientInstrumentation()
                    .AddSource("Application.Edge.ServiceWrapper");
            });

        applicationBuilder.Builder.Services.AddSingleton<IAppLoggerReader, SerilogLoggerReader>();

        return applicationBuilder;
    }

    public static void AddLoggerMiddlewares(this ApplicationHost applicationHost)
    {
        ArgumentNullException.ThrowIfNull(applicationHost);

        if (applicationHost.Host is not WebApplication webApplication)
        {
            return;
        }

        webApplication.UseSerilogRequestLogging();
    }

    private static LoggerConfiguration Configure(LoggerConfiguration configuration, ApplicationHostBuilder applicationBuilder, IReadOnlyDictionary<string, object?> scopeMap)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(applicationBuilder);
        ArgumentNullException.ThrowIfNull(scopeMap);

        var logEventLevel = applicationBuilder.Configuration.GetLoggerLevel();

        configuration
            .MinimumLevel.Is(logEventLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.With(new DefaultLogEnricher(scopeMap))
            .Enrich.FromLogContext()
            .WriteTo.Console(new ExpressionTemplate(
                "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}",
                theme: ConsoleTheme));

        if (applicationBuilder.Configuration.ContainsRefValue("OTEL_EXPORTER_OTLP_ENDPOINT"))
        {
            configuration.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = applicationBuilder.Configuration.GetRefValue("OTEL_EXPORTER_OTLP_ENDPOINT");
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = Build.Constants.AppTitle,
                };
            });
        }

        return configuration;
    }
}
