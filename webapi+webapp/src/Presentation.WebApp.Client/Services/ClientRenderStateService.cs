using Presentation.WebApp.Enums;
using Presentation.WebApp.Services;

namespace Presentation.WebApp.Client.Services;

/// <summary>
/// Client-side (WebAssembly) implementation of IRenderStateService.
/// Always returns CSR since WASM components are always client-side rendered.
/// </summary>
public class ClientRenderStateService : IRenderStateService
{
    /// <inheritdoc />
    public Guid ServiceUid { get; } = Guid.NewGuid();

    /// <summary>
    /// Always returns CSR for WebAssembly components.
    /// By the time WASM is running, pre-rendering is complete.
    /// </summary>
    public RenderState RenderState => RenderState.CSR;
}
