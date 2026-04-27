using Application.AppEnvironment.Extensions;
using Application.Credential.Extensions;
using Application.HelloWorld.Extensions;
using Application.Shared.Extensions;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public class Application : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddAppEnvironmentServices();
        services.AddCredentialServices();
        services.AddSharedServices();
        services.AddHelloWorldServices();
    }
}
