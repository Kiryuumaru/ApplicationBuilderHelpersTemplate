using Microsoft.AspNetCore.Http;
using Presentation.WebApp.Enums;
using Presentation.WebApp.Services;

namespace Presentation.WebApp.Server.Services;

/// <summary>
/// Server-side implementation of IRenderStateService.
/// Detects pre-rendering by checking if HttpContext exists and if the response has started.
/// </summary>
public class ServerRenderStateService : IRenderStateService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <inheritdoc />
    public Guid ServiceUid { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public RenderState RenderState
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            
            // If HttpContext exists and response hasn't started, we're pre-rendering
            // Once the response starts streaming, we're in SSR mode
            if (httpContext is not null && !httpContext.Response.HasStarted)
            {
                return RenderState.PreRender;
            }
            
            return RenderState.SSR;
        }
    }

    /// <summary>
    /// Initializes a new instance of the ServerRenderStateService class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor for detecting render state.</param>
    public ServerRenderStateService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
}
