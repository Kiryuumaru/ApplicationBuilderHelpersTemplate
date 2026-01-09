using Infrastructure.EFCore.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Sqlite.Extensions;

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
        // Disable EF Core's internal service provider caching to ensure each factory
        // instance uses its own set of entity configurations. This prevents issues
        // where different configuration sets share a cached model.
        optionsBuilder.EnableServiceProviderCaching(false);
        return new SqliteDbContext(optionsBuilder.Options, _configurations);
    }
}
