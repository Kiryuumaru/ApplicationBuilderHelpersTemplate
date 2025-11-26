using Application.LocalStore.Interfaces;
using Infrastructure.Sqlite.Interfaces;
using Infrastructure.Sqlite.LocalStore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Sqlite.LocalStore.Extensions;

internal static class SqliteLocalStoreServiceCollectionExtensions
{
    public static IServiceCollection AddSqliteLocalStore(this IServiceCollection services)
    {
        services.AddScoped<ILocalStoreService, SqliteLocalStoreService>();
        services.AddSingleton<IDatabaseBootstrap, LocalStoreTableInitializer>();
        return services;
    }
}
