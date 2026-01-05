using Application.Authorization.Interfaces;
using Application.Identity.Interfaces;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.WebApi.Controllers.V1.Iam;

/// <summary>
/// Controller for Identity and Access Management (IAM) operations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/iam")]
[Produces("application/json")]
[Authorize]
[Tags("IAM")]
public partial class IamController(
    IUserProfileService userProfileService,
    IUserAuthorizationService userAuthorizationService,
    IUserRegistrationService userRegistrationService,
    IPasswordService passwordService,
    IRoleService roleService) : ControllerBase
{
}
