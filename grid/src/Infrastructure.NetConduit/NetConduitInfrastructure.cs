using ApplicationBuilderHelpers;
using Infrastructure.NetConduit.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.NetConduit;

public class NetConduitInfrastructure : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddNetConduitServices();
    }
}
