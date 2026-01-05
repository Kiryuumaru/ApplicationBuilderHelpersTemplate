using Application.Authorization.Services;
using ApplicationBuilderHelpers;
using Infrastructure.EFCore.Identity.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EFCore.Identity;

public class EFCoreIdentityInfrastructure : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddEFCoreIdentity();
        
        // Register JWT token services with production credentials
        services.AddJwtTokenServices("GOAT_CLOUD", async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var cloudAuthenticationServices = scope.ServiceProvider.GetRequiredService<CredentialsService>();
            var cloudCreds = await cloudAuthenticationServices.GetCredentials(ct);
            return cloudCreds.JwtConfiguration;
        });
    }
}
