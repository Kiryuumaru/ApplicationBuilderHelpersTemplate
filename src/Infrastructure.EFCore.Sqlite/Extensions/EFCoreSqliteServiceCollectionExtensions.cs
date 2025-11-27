using ApplicationBuilderHelpers.Extensions;
using Infrastructure.EFCore.Interfaces;
using Infrastructure.EFCore.Sqlite.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Sqlite.Extensions;

public static class EFCoreSqliteServiceCollectionExtensions
{
    private const string DefaultConnectionString = "Data Source=app.db";

    public static IServiceCollection AddEFCoreSqlite(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRefValueOrDefault("SQLITE_CONNECTION_STRING", DefaultConnectionString);
        return services.AddEFCoreSqlite(connectionString);
    }

    public static IServiceCollection AddEFCoreSqlite(this IServiceCollection services, string connectionString)
    {
        // For in-memory databases with shared cache, keep one connection open
        // to prevent the database from being destroyed when all connections close
        services.AddSingleton(new SqliteConnectionHolder(connectionString));
        
        // Register DbContext with factory pattern that injects entity configurations
        services.AddDbContext<SqliteDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
        });

        // Register factory that creates DbContext with configurations
        services.AddSingleton<IDbContextFactory<SqliteDbContext>>(sp =>
        {
            var configurations = sp.GetServices<IEFCoreEntityConfiguration>();
            return new SqliteDbContextFactory(connectionString, configurations);
        });

        // Register IDbContextFactory<EFCoreDbContext> for persistence-ignorant consumers
        services.AddSingleton<IDbContextFactory<EFCoreDbContext>>(sp => 
            new EFCoreDbContextFactoryAdapter(sp.GetRequiredService<IDbContextFactory<SqliteDbContext>>()));

        // Also register the base DbContext for scoped usage
        services.AddScoped<EFCoreDbContext>(sp => sp.GetRequiredService<SqliteDbContext>());

        services.AddScoped<IEFCoreDatabaseBootstrap, SqliteDatabaseBootstrap>();

        return services;
    }
}

/// <summary>
/// Factory for creating SqliteDbContext instances with registered entity configurations.
/// </summary>
internal sealed class SqliteDbContextFactory : IDbContextFactory<SqliteDbContext>
{
    private readonly string _connectionString;
    private readonly IEnumerable<IEFCoreEntityConfiguration> _configurations;

    public SqliteDbContextFactory(string connectionString, IEnumerable<IEFCoreEntityConfiguration> configurations)
    {
        _connectionString = connectionString;
        _configurations = configurations;
    }

    public SqliteDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqliteDbContext>();
        optionsBuilder.UseSqlite(_connectionString);
        return new SqliteDbContext(optionsBuilder.Options, _configurations);
    }
}

/// <summary>
/// Adapter to allow IDbContextFactory&lt;EFCoreDbContext&gt; registration from specific DbContext factories.
/// </summary>
internal sealed class EFCoreDbContextFactoryAdapter(IDbContextFactory<SqliteDbContext> innerFactory) : IDbContextFactory<EFCoreDbContext>
{
    public EFCoreDbContext CreateDbContext() => innerFactory.CreateDbContext();
}
