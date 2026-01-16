using Infrastructure.EFCore.Sqlite.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Sqlite.Client.Extensions;

public static class EFCoreSqliteClientServiceCollectionExtensions
{
    /// <summary>
    /// SQLite in WASM defaults to in-memory storage. OPFS (Origin Private File System) enables
    /// persistent storage in browsers that support it (Chrome 86+, Firefox 111+, Safari 15.2+).
    /// </summary>
    private const string DefaultWasmConnectionString = "Data Source=app.db";

    /// <summary>
    /// Adds SQLite EF Core infrastructure configured for WASM/browser environments.
    /// Uses OPFS for persistence where supported.
    /// </summary>
    public static IServiceCollection AddEFCoreSqliteClient(this IServiceCollection services)
    {
        return services.AddEFCoreSqliteClient(DefaultWasmConnectionString);
    }

    /// <summary>
    /// Adds SQLite EF Core infrastructure configured for WASM/browser environments
    /// with a custom connection string.
    /// </summary>
    public static IServiceCollection AddEFCoreSqliteClient(this IServiceCollection services, string connectionString)
    {
        return services.AddEFCoreSqlite(connectionString);
    }
}
