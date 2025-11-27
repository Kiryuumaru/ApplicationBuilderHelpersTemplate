using ApplicationBuilderHelpers;
using Infrastructure.EFCore.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore;

public class InfrastructureEFCore : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddEFCoreInfrastructure();
    }
}
