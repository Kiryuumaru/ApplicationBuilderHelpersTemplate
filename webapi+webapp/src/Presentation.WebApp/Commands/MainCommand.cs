using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Presentation.WebApp.Components;

namespace Presentation.WebApp.Commands;

[Command("Main subcommand.")]
internal class MainCommand : Build.BaseCommand<WebApplicationBuilder>
{
    [CommandOption("urls", Description = "Server listening URLs (semicolon-separated)", EnvironmentVariable = "ASPNETCORE_URLS")]
    public required string Urls { get; set; }

    protected override ValueTask<WebApplicationBuilder> ApplicationBuilder(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls(Urls.Split(';', StringSplitOptions.RemoveEmptyEntries));

        return new ValueTask<WebApplicationBuilder>(builder);
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();
    }

    public override void AddMappings(ApplicationHost applicationHost, IHost host)
    {
        base.AddMappings(applicationHost, host);

        var app = (WebApplication)host;

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Presentation.WebApp.Client._Imports).Assembly);
    }
}
