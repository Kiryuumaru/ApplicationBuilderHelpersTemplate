using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Client.Sqlite;

/// <summary>
/// Client-side EFCore Sqlite infrastructure composition.
/// Composes: EFCore.Sqlite + LocalStore only (no Identity)
/// </summary>
public class EFCoreClientSqliteInfrastructure : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        // Client-specific Sqlite configuration can be added here
    }
}
