using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Sqlite.Services;

/// <summary>
/// Adapter to allow IDbContextFactory&lt;EFCoreDbContext&gt; registration from specific DbContext factories.
/// </summary>
internal sealed class EFCoreDbContextFactoryAdapter(IDbContextFactory<SqliteDbContext> innerFactory) : IDbContextFactory<EFCoreDbContext>
{
    public EFCoreDbContext CreateDbContext() => innerFactory.CreateDbContext();
}
