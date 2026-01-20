using Microsoft.Extensions.DependencyInjection;
using Presentation.WebApp.Client.Services;
using Presentation.WebApp.Services;

namespace Presentation.WebApp.Client.Extensions;

/// <summary>
/// Extension methods for registering client-side render state services.
/// </summary>
public static class RenderStateServiceCollectionExtensions
{
    /// <summary>
    /// Adds the client-side (WASM) render state service to the service collection.
    /// This service always reports CSR mode since WASM is always client-rendered.
    /// </summary>
    public static IServiceCollection AddClientRenderStateServices(this IServiceCollection services)
    {
        services.AddScoped<IRenderStateService, ClientRenderStateService>();
        return services;
    }
}
