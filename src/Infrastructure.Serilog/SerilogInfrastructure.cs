using Application.Logger.Interfaces;
using ApplicationBuilderHelpers;
using Infrastructure.Serilog.Common;
using Infrastructure.Serilog.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Infrastructure.Serilog;

public class SerilogInfrastructure : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddTransient<ILoggerReader, SerilogLoggerReader>();

        if (applicationBuilder.Builder is WebApplicationBuilder webApplicationBuilder)
        {
            webApplicationBuilder.Host
                .UseSerilog((context, loggerConfiguration) => LoggerBuilder.Configure(loggerConfiguration, applicationBuilder.Configuration));
        }
        else
        {
            services.AddLogging(config =>
            {
                config.ClearProviders();
                config.AddSerilog(LoggerBuilder.Configure(new LoggerConfiguration(), applicationBuilder.Configuration).CreateLogger());
            });
        }

        Log.Logger = LoggerBuilder.Configure(new LoggerConfiguration(), applicationBuilder.Configuration).CreateLogger();
    }

    public override void AddMiddlewares(ApplicationHost applicationHost, IHost host)
    {
        base.AddMiddlewares(applicationHost, host);

        if (host is IApplicationBuilder webApplicationBuilder)
        {
            webApplicationBuilder.UseSerilogRequestLogging();
        }
    }
}
