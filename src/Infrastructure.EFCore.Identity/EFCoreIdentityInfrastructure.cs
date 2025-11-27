using ApplicationBuilderHelpers;
using Infrastructure.EFCore.Identity.Extensions;
using Infrastructure.EFCore.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Identity;

public class EFCoreIdentityInfrastructure : EFCoreSqliteInfrastructure
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddEFCoreIdentityStores();
    }
}
