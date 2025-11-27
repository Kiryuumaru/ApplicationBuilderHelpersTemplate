using ApplicationBuilderHelpers;
using Infrastructure.EFCore.LocalStore.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.LocalStore;

public class EFCoreLocalStoreInfrastructure : InfrastructureEFCore
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddEFCoreLocalStore();
    }
}
