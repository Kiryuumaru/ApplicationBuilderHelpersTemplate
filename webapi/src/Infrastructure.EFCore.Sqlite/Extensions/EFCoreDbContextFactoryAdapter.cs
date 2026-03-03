using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Sqlite.Extensions;

internal sealed class EFCoreDbContextFactoryAdapter(IDbContextFactory<SqliteDbContext> innerFactory) : IDbContextFactory<EFCoreDbContext>
{
    public EFCoreDbContext CreateDbContext() => innerFactory.CreateDbContext();
}
