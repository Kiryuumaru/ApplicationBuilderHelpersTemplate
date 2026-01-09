using Application.Server.Identity.Extensions;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Server;

/// <summary>
/// Server-side application configuration that extends the shared Application
/// with server-specific services like Identity.
/// </summary>
public class ApplicationServer : global::Application.Application
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddIdentityServices();
    }
}
