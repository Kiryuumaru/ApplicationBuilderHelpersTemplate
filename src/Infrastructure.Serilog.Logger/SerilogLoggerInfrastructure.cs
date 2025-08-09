using ApplicationBuilderHelpers;
using Infrastructure.Serilog.Logger.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Serilog.Logger;

public class SerilogLoggerInfrastructure : ApplicationDependency
{
    public override void AddConfigurations(ApplicationHostBuilder applicationBuilder, IConfiguration configuration)
    {
        base.AddConfigurations(applicationBuilder, configuration);

        applicationBuilder.AddLoggerConfiguration();
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        applicationBuilder.AddLoggerServices();
    }

    public override void AddMiddlewares(ApplicationHost applicationHost, IHost host)
    {
        base.AddMiddlewares(applicationHost, host);

        applicationHost.AddLoggerMiddlewares();
    }
}
