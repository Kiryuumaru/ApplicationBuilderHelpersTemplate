using ApplicationBuilderHelpers;
using Application.LocalStore.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Application;

public class Application : ApplicationDependency
{
    public override void CommandPreparation(ApplicationBuilder applicationBuilder)
    {
        base.CommandPreparation(applicationBuilder);

        // Application-level command preparation only
        // Infrastructure setup moved to Infrastructure project
    }

    public override void BuilderPreparation(ApplicationHostBuilder applicationBuilder)
    {
        base.BuilderPreparation(applicationBuilder);
    }

    public override void AddConfigurations(ApplicationHostBuilder applicationBuilder, IConfiguration configuration)
    {
        base.AddConfigurations(applicationBuilder, configuration);

        // Application-level configurations only
        // Infrastructure configurations moved to Infrastructure project
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddLocalStoreServices();

        // Application-level services only
        // Infrastructure services moved to Infrastructure project

        // Add use case handlers, domain services, application services here
        // Example: services.AddScoped<IUserService, UserService>();
    }

    public override void AddMiddlewares(ApplicationHost applicationHost, IHost host)
    {
        base.AddMiddlewares(applicationHost, host);

        // Application-level middlewares only
        // Infrastructure middlewares moved to Infrastructure project
    }

    public override void RunPreparation(ApplicationHost applicationHost)
    {
        base.RunPreparation(applicationHost);
    }
}
