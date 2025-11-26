using ApplicationBuilderHelpers;
using Infrastructure.Sqlite.Identity.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Sqlite.Identity;

public class SqliteIdentityInfrastructure : SqliteInfrastructure
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddSqliteIdentityStores();
    }
}
