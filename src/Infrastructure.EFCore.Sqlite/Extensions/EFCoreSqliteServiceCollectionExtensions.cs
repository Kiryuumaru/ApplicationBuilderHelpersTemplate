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
        
        // Register DbContext with factory pattern that injects entity configurations
        services.AddDbContext<SqliteDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
        });

        // Register factory that creates DbContext with configurations
        services.AddSingleton<IDbContextFactory<SqliteDbContext>, SqliteDbContextFactory>();

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
    private readonly IServiceProvider _serviceProvider;
    private readonly string _connectionString;

    public SqliteDbContextFactory(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _connectionString = configuration.GetRefValueOrDefault("SQLITE_CONNECTION_STRING", "Data Source=app.db");
    }

    public SqliteDbContext CreateDbContext()
    {
        var configurations = _serviceProvider.GetServices<IEFCoreEntityConfiguration>();
        var optionsBuilder = new DbContextOptionsBuilder<SqliteDbContext>();
        optionsBuilder.UseSqlite(_connectionString);
        return new SqliteDbContext(optionsBuilder.Options, configurations);
    }
}

/// <summary>
/// Adapter to allow IDbContextFactory&lt;EFCoreDbContext&gt; registration from specific DbContext factories.
/// </summary>
internal sealed class EFCoreDbContextFactoryAdapter(IDbContextFactory<SqliteDbContext> innerFactory) : IDbContextFactory<EFCoreDbContext>
{
    public EFCoreDbContext CreateDbContext() => innerFactory.CreateDbContext();
}
