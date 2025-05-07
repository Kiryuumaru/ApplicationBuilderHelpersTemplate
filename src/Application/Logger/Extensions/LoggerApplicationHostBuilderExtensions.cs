using Application.Logger.Common;
using Application.Logger.Interfaces;
using Application.Logger.Services;
using ApplicationBuilderHelpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Application.Logger.Extensions;

public static class LoggerApplicationHostBuilderExtensions
{
    public static ApplicationHostBuilder AddLoggerServices(this ApplicationHostBuilder applicationBuilder)
    {
        applicationBuilder.Services.AddTransient<ILoggerReader, SerilogLoggerReader>();

        if (applicationBuilder.Builder is WebApplicationBuilder webApplicationBuilder)
        {
            webApplicationBuilder.Host
                .UseSerilog((context, loggerConfiguration) => LoggerBuilder.Configure(loggerConfiguration, applicationBuilder.Configuration));
        }
        else
        {
            applicationBuilder.Services.AddLogging(config =>
            {
                config.ClearProviders();
                config.AddSerilog(LoggerBuilder.Configure(new LoggerConfiguration(), applicationBuilder.Configuration).CreateLogger());
            });
        }

        Log.Logger = LoggerBuilder.Configure(new LoggerConfiguration(), applicationBuilder.Configuration).CreateLogger();

        return applicationBuilder;
    }

    public static void AddLoggerMiddlewares(this ApplicationHost applicationHost)
    {
        if (applicationHost.Host is IApplicationBuilder webApplicationBuilder)
        {
            webApplicationBuilder.UseSerilogRequestLogging();
        }
    }
}
