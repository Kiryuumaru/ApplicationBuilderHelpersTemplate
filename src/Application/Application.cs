using Application.Abstractions.Application;
using Application.AppEnvironment.Extensions;
using Application.Common.Extensions;
using Application.Identity.Extensions;
using Application.LocalStore.Extensions;
using Application.Logger.Extensions;
using Application.NativeCmd.Extensions;
using ApplicationBuilderHelpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Application;

public class Application : ApplicationDependency
{
    protected Dictionary<string, object?> LogDefaultScopeMap { get; set; } = [];

    public override void AddConfigurations(ApplicationHostBuilder applicationBuilder, IConfiguration configuration)
    {
        base.AddConfigurations(applicationBuilder, configuration);

        applicationBuilder.AddLoggerConfiguration(LogDefaultScopeMap);
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        applicationBuilder.AddLoggerServices();

        services.AddHttpClient(Options.DefaultName, (sp, client) =>
        {
            using var scope = sp.CreateScope();
            var applicationConstans = scope.ServiceProvider.GetRequiredService<IApplicationConstants>();
            client.DefaultRequestHeaders.Add("Client-Agent", applicationConstans.AppName);
        });

        applicationBuilder.Builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        applicationBuilder.Services.AddServiceDiscovery();

        applicationBuilder.Builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            //http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        services.AddCommonServices();
        services.AddAppEnvironmentServices();
        services.AddNativeCmdServices();
        services.AddLocalStoreServices();
        services.AddIdentityServices();
    }

    public override void AddMiddlewares(ApplicationHost applicationHost, IHost host)
    {
        base.AddMiddlewares(applicationHost, host);

        applicationHost.AddLoggerMiddlewares();

        if (applicationHost.Host is WebApplication webApplicationBuilder)
        {
            if (webApplicationBuilder.Environment.IsDevelopment())
            {
                webApplicationBuilder.MapHealthChecks("/health");
                webApplicationBuilder.MapHealthChecks("/alive", new HealthCheckOptions
                {
                    Predicate = r => r.Tags.Contains("live")
                });
            }
        }
    }

    public override void RunPreparation(ApplicationHost applicationHost)
    {
        base.RunPreparation(applicationHost);
    }
}
