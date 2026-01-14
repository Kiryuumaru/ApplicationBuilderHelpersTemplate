using ApplicationBuilderHelpers.Attributes;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Presentation.WebApp.Client.Controllers;

[Command("Main subcommand.")]
internal class MainCommand : Build.BaseCommand<WebAssemblyHostBuilderWrapper>
{
    protected override ValueTask<WebAssemblyHostBuilderWrapper> ApplicationBuilder(CancellationToken stoppingToken)
    {
        var builder = new WebAssemblyHostBuilderWrapper(WebAssemblyHostBuilder.CreateDefault());

        return new ValueTask<WebAssemblyHostBuilderWrapper>(builder);
    }


}
