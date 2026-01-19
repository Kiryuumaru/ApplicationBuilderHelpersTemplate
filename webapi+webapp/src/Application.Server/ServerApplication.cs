using Application.Server.Authorization.Extensions;
using Application.Server.Identity.Extensions;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Server;

public class ServerApplication : Application
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddAuthorizationServices();
        services.AddIdentityServices();
    }
}
