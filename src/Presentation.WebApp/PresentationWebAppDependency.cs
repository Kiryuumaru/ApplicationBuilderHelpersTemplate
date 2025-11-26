using ApplicationBuilderHelpers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Presentation.WebApp.Components.Account;
using Domain.Identity.Models;

using Microsoft.AspNetCore.Builder;

namespace Presentation.WebApp;

public class PresentationWebAppDependency : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        services.AddCascadingAuthenticationState();
        services.AddScoped<IdentityUserAccessor>();
        services.AddScoped<IdentityRedirectManager>();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
        services.AddSingleton<IEmailSender<User>, IdentityNoOpEmailSender>();
    }

    public override void AddMappings(ApplicationHost applicationHost, IHost host)
    {
        base.AddMappings(applicationHost, host);

        if (applicationHost.Host is WebApplication app)
        {
            app.MapAdditionalIdentityEndpoints();
        }
    }
}
