using ApplicationBuilderHelpers.Extensions;
using Infrastructure.EFCore.Interfaces;
using Infrastructure.EFCore.Sqlite.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Sqlite.Extensions;

internal static class EFCoreSqliteServiceCollectionExtensions
{
    private const string DefaultConnectionString = "Data Source=app.db";

    public static IServiceCollection AddEFCoreSqlite(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRefValueOrDefault("SQLITE_CONNECTION_STRING", DefaultConnectionString);
        
        // For in-memory databases with shared cache, keep one connection open
        // to prevent the database from being destroyed when all connections close
        services.AddSingleton(new SqliteConnectionHolder(connectionString));
        
        // Use AddDbContextFactory which properly handles the lifetime
        services.AddDbContextFactory<SqliteDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        // Register IDbContextFactory<EFCoreDbContext> for persistence-ignorant consumers
        services.AddScoped<IDbContextFactory<EFCoreDbContext>>(sp => 
            new EFCoreDbContextFactoryAdapter(sp.GetRequiredService<IDbContextFactory<SqliteDbContext>>()));

        // Also register the DbContext itself for scoped usage
        services.AddScoped<SqliteDbContext>(sp => sp.GetRequiredService<IDbContextFactory<SqliteDbContext>>().CreateDbContext());
        services.AddScoped<EFCoreDbContext>(sp => sp.GetRequiredService<SqliteDbContext>());

        services.AddScoped<IEFCoreDatabaseBootstrap, SqliteDatabaseBootstrap>();

        return services;
    }
}

/// <summary>
/// Adapter to allow IDbContextFactory&lt;EFCoreDbContext&gt; registration from specific DbContext factories.
/// </summary>
internal sealed class EFCoreDbContextFactoryAdapter(IDbContextFactory<SqliteDbContext> innerFactory) : IDbContextFactory<EFCoreDbContext>
{
    public EFCoreDbContext CreateDbContext() => innerFactory.CreateDbContext();
}
