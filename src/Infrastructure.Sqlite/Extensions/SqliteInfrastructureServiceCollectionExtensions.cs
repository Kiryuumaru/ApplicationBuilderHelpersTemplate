using Application.LocalStore.Interfaces;
using ApplicationBuilderHelpers.Extensions;
using Infrastructure.Sqlite.Services;
using Infrastructure.Sqlite.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Sqlite.Extensions;

internal static class SqliteInfrastructureServiceCollectionExtensions
{
    private const string DefaultConnectionString = "Data Source=app.db";

    public static IServiceCollection AddSqliteInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRefValueOrDefault("SQLITE_CONNECTION_STRING", DefaultConnectionString);
        services.AddSingleton(new SqliteConnectionFactory(connectionString));
        services.AddSingleton<DatabaseInitializationState>();
        services.AddSingleton<IDatabaseInitializationState>(sp => sp.GetRequiredService<DatabaseInitializationState>());
        services.AddHostedService<SqliteDatabaseBootstrapperWorker>();
        return services;
    }
}
