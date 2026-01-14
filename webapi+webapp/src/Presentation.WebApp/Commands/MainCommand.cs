using ApplicationBuilderHelpers.Attributes;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Presentation.WebApp.Commands;

[Command("Main subcommand.")]
internal class MainCommand : Build.BaseCommand<WebAssemblyHostBuilderWrapper>
{
    [CommandOption("urls", Description = "Server listening URLs (semicolon-separated)", EnvironmentVariable = "ASPNETCORE_URLS")]
    public string? Urls { get; set; }

    protected override ValueTask<WebAssemblyHostBuilderWrapper> ApplicationBuilder(CancellationToken stoppingToken)
    {
        var builder = new WebAssemblyHostBuilderWrapper(WebAssemblyHostBuilder.CreateDefault());

        // Configure URLs if specified
        if (!string.IsNullOrEmpty(Urls))
        {
            builder.WebHost.UseUrls(Urls.Split(';', StringSplitOptions.RemoveEmptyEntries));
        }

        return new ValueTask<WebAssemblyHostBuilderWrapper>(builder);
    }
}
