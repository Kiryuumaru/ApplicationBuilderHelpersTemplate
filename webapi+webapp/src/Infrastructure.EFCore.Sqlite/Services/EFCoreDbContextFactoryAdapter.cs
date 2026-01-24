using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Sqlite.Services;

[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "EF Core inherently uses reflection. NativeAOT support requires compiled models.")]
internal sealed class EFCoreDbContextFactoryAdapter(IDbContextFactory<SqliteDbContext> innerFactory) : IDbContextFactory<EFCoreDbContext>
{
    public EFCoreDbContext CreateDbContext() => innerFactory.CreateDbContext();
}
