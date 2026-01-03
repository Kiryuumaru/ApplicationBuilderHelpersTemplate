using ApplicationBuilderHelpers;
using Fido2NetLib;
using Infrastructure.Passkeys.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Passkeys;

public class PasskeysInfrastructure : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        // Get configuration
        var configuration = applicationBuilder.Configuration;

        // Configure Fido2 from configuration or use defaults
        services.AddPasskeyInfrastructure(config =>
        {
            var fido2Section = configuration.GetSection("Fido2");

            config.ServerName = fido2Section["ServerName"] ?? "ProjectOffworlder";
            config.ServerDomain = fido2Section["ServerDomain"] ?? "localhost";

            var originsSection = fido2Section.GetSection("Origins");
            if (originsSection.Exists())
            {
                config.Origins = originsSection.Get<HashSet<string>>() ?? new HashSet<string> { "https://localhost" };
            }
            else
            {
                config.Origins = new HashSet<string> { "https://localhost" };
            }
        });
    }
}
