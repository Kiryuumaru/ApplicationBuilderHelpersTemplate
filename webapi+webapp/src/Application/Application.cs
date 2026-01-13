using Application.AppEnvironment.Extensions;
using Application.Common.Extensions;
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
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddCommonServices();
        services.AddAppEnvironmentServices();
        services.AddNativeCmdServices();
        services.AddLocalStoreServices();
    }
}
