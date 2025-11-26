using ApplicationBuilderHelpers;
using Infrastructure.Sqlite.LocalStore.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Sqlite.LocalStore;

public class SqliteLocalStoreInfrastructure : SqliteInfrastructure
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddSqliteLocalStore();
    }
}
