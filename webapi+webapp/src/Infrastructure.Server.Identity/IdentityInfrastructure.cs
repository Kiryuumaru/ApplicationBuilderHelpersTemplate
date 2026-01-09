using Application.Server.Authorization.Services;
using ApplicationBuilderHelpers;
using Infrastructure.Server.Identity.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Server.Identity;

public class IdentityInfrastructure : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddIdentityCoreServices();

        // Register JWT token services using CredentialsService
        services.AddJwtTokenServices(async (sp, ct) =>
        {
            var credentialsService = sp.GetRequiredService<CredentialsService>();
            var credentials = await credentialsService.GetCredentials(ct);
            return credentials.JwtConfiguration;
        });

        // Configure JWT Bearer authentication
        services.AddJwtBearerConfiguration();
    }
}
