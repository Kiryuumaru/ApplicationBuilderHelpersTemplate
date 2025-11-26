using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Sqlite.Extensions;

public static class SqliteInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddSqliteInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(new SqliteConnectionFactory(connectionString));
        services.AddHostedService<SqliteDatabaseBootstrapperWorker>();
        return services;
    }
}
