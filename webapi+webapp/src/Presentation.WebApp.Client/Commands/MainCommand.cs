using Application.Client.Authentication.Interfaces.Infrastructure;
using Application.LocalStore.Interfaces.Infrastructure;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Presentation.WebApp.Client.Services;

namespace Presentation.WebApp.Client.Commands;

[Command("Main subcommand.")]
internal class MainCommand : Build.BaseCommand<WebAssemblyHostBuilderWrapper>
{
    protected override ValueTask<WebAssemblyHostBuilderWrapper> ApplicationBuilder(CancellationToken stoppingToken)
    {
        var builder = new WebAssemblyHostBuilderWrapper(WebAssemblyHostBuilder.CreateDefault());

        return new ValueTask<WebAssemblyHostBuilderWrapper>(builder);
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddBlazoredLocalStorage();

        services.AddScoped<ITokenStorage, BlazoredLocalStorageStorage>();
        services.AddScoped<ILocalStoreService, BlazoredLocalStorageStorage>();

        services.AddScoped<AuthenticationStateProvider, BlazorAuthStateProvider>();

        services.AddAuthorizationCore();
    }
}
