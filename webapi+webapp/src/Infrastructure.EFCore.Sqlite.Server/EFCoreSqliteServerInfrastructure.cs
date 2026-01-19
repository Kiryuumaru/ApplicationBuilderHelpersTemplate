using ApplicationBuilderHelpers;

namespace Infrastructure.EFCore.Sqlite.Server;

/// <summary>
/// Server-side EFCore Sqlite infrastructure composition.
/// Connection string is resolved via GetRefValueOrDefault which checks
/// IConfiguration then environment variables, with fallback to "Data Source=app.db".
/// </summary>
public class EFCoreSqliteServerInfrastructure : ApplicationDependency
{
}
