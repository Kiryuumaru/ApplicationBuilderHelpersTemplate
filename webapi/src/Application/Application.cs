using Application.AppEnvironment.Extensions;
using Application.Common.Extensions;
using Application.Identity.Extensions;
using Application.LocalStore.Extensions;
using Application.Logger.Extensions;
using Application.NativeCmd.Extensions;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Application;

public class Application : ApplicationDependency
{
    protected Dictionary<string, object?> LogDefaultScopeMap { get; set; } = [];

    public override void AddConfigurations(ApplicationHostBuilder applicationBuilder, IConfiguration configuration)
    {
        base.AddConfigurations(applicationBuilder, configuration);

        applicationBuilder.AddLoggerConfiguration(LogDefaultScopeMap);
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        applicationBuilder.AddLoggerServices();

        services.AddCommonServices();
        services.AddAppEnvironmentServices();
        services.AddNativeCmdServices();
        services.AddLocalStoreServices();
        services.AddIdentityServices();
    }

    public override void AddMiddlewares(ApplicationHost applicationHost, IHost host)
    {
        base.AddMiddlewares(applicationHost, host);

        applicationHost.AddLoggerMiddlewares();
    }

    public override void RunPreparation(ApplicationHost applicationHost)
    {
        base.RunPreparation(applicationHost);
    }
}
