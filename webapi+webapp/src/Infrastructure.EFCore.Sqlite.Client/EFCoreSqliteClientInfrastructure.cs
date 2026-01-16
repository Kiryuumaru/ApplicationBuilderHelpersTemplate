using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Sqlite.Client;

/// <summary>
/// Client-side EFCore Sqlite infrastructure composition.
/// Provides SQLite with OPFS persistence for WASM/browser environments.
/// Does NOT compose features (LocalStore, Identity) - those are composed at Presentation layer.
/// </summary>
public class EFCoreSqliteClientInfrastructure : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        // Client-specific Sqlite configuration (OPFS) will be added here
    }
}
