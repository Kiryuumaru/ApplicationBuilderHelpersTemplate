using ApplicationBuilderHelpers;
using Infrastructure.EFCore.Sqlite.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Sqlite.Client;

/// <summary>
/// Client-side EFCore Sqlite infrastructure composition.
/// Provides SQLite with OPFS persistence for WASM/browser environments.
/// Does NOT compose features (LocalStore, Identity) - those are composed at Presentation layer.
/// </summary>
public class EFCoreSqliteClientInfrastructure : ApplicationDependency
{
    public override void AddConfigurations(ApplicationHostBuilder applicationBuilder, IConfiguration configuration)
    {
        base.AddConfigurations(applicationBuilder, configuration);

        configuration.SetSqliteConnectionString("Data Source=app.db");
    }
}
