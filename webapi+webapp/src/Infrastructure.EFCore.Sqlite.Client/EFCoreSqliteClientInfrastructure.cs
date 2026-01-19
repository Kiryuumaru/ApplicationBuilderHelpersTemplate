using ApplicationBuilderHelpers;

namespace Infrastructure.EFCore.Sqlite.Client;

/// <summary>
/// Client-side EFCore Sqlite infrastructure composition.
/// Provides SQLite with OPFS persistence for WASM/browser environments.
/// Connection string is resolved via GetRefValueOrDefault which checks
/// IConfiguration then environment variables, with fallback to "Data Source=app.db".
/// </summary>
public class EFCoreSqliteClientInfrastructure : ApplicationDependency
{
}
