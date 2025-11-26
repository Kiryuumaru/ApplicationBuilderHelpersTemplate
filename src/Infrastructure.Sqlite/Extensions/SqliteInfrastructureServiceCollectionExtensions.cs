using ApplicationBuilderHelpers.Extensions;
using Infrastructure.Sqlite.Services;
using Infrastructure.Sqlite.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Sqlite.Extensions;

public static class SqliteInfrastructureServiceCollectionExtensions
{
    private const string DefaultConnectionString = "Data Source=app.db";

    public static IServiceCollection AddSqliteInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRefValueOrDefault("SQLITE_CONNECTION_STRING", DefaultConnectionString);
        services.AddSingleton(new SqliteConnectionFactory(connectionString));
        services.AddHostedService<SqliteDatabaseBootstrapperWorker>();
        return services;
    }
}
