using Application.AssetResolver.Extensions;
using Application.AssetResolver.Services;
using Application.Configuration.Interfaces;
using Application.LocalStore.Extensions;
using Application.LocalStore.Services;
using Application.NativeCmd.Extensions;
using Application.NativeCmd.Services;
using Application.NativeServiceInstaller.Extensions;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Application;

public class Application : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddHttpClient(Options.DefaultName, (sp, client) =>
        {
            using var scope = sp.CreateScope();
            var applicationConstans = scope.ServiceProvider.GetRequiredService<IApplicationConstants>();
            client.DefaultRequestHeaders.Add("Client-Agent", applicationConstans.AppName);
        });

        services.AddCmdServices();
        services.AddLocalStoreServices();
        services.AddAssetResolverServices();
        services.AddNativeServiceInstallerServices();
    }

    public override void RunPreparation(ApplicationHost applicationHost)
    {
        base.RunPreparation(applicationHost);
    }
}
