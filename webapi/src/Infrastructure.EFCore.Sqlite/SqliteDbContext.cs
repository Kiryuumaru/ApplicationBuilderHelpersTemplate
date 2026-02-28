using Infrastructure.EFCore.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Sqlite;

public class SqliteDbContext : EFCoreDbContext
{
    public SqliteDbContext(DbContextOptions<SqliteDbContext> options, IEnumerable<IEFCoreEntityConfiguration> configurations) 
        : base(options, configurations)
    {
    }
}

