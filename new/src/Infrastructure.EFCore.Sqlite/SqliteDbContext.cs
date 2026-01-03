using Infrastructure.EFCore.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Sqlite;

/// <summary>
/// SQLite-specific DbContext that inherits from the base EFCoreDbContext.
/// </summary>
public class SqliteDbContext : EFCoreDbContext
{
    public SqliteDbContext(DbContextOptions<SqliteDbContext> options, IEnumerable<IEFCoreEntityConfiguration> configurations) 
        : base(options, configurations)
    {
    }
}

