using Asp.Versioning;
using Domain.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Controllers.V1;

/// <summary>
/// Test controller for development purposes.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/hello")]
[Produces("application/json")]
[Tags("Hello")]
[Authorize]
public class HelloController : ControllerBase
{
    /// <summary>
    /// Test endpoint for development purposes.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success result.</returns>
    [RequiredPermission(PermissionIds.Api.Portfolio.Accounts.List.Identifier)]
    [HttpPost("{deviceId}/{appId}/{assetName}")]
    [Consumes("multipart/form-data")]
    [DisableRequestSizeLimit]
    [ProducesResponseType(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(409)]
    [ProducesResponseType<ProblemDetails>(413)]
    [ProducesResponseType<ProblemDetails>(500)]
    public async Task<IActionResult> TestEndpoint(
        [FromRoute, Required, PermissionParameter(PermissionIds.Api.Portfolio.Accounts.List.UserIdParameter)] string userId,
        CancellationToken cancellationToken = default)
    {
        return Ok();
    }
}
