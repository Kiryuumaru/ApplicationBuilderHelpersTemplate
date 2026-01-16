using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Sqlite.Server;

/// <summary>
/// Server-side EFCore Sqlite infrastructure composition.
/// Composes: EFCore.Sqlite + Server.Identity + LocalStore
/// </summary>
public class EFCoreSqliteServerInfrastructure : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        // Server-specific Sqlite configuration can be added here
    }
}
