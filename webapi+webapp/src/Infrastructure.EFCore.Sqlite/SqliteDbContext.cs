using System.Diagnostics.CodeAnalysis;
using Infrastructure.EFCore.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Sqlite;

[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "EF Core inherently uses reflection. NativeAOT support requires compiled models.")]
public class SqliteDbContext(DbContextOptions<SqliteDbContext> options, IEnumerable<IEFCoreEntityConfiguration> configurations) : EFCoreDbContext(options, configurations)
{
}
