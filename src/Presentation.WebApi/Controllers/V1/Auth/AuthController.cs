using Application.Authorization.Interfaces;
using Application.Common.Interfaces;
using Application.Identity.Interfaces;
using Application.Identity.Models;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Domain.Authorization.ValueObjects;
using Domain.Identity.Enums;
using Domain.Identity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Models.Requests;
using Presentation.WebApi.Models.Responses;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Presentation.WebApi.Controllers.V1;

/// <summary>
/// Controller for authentication operations including login, registration, and token management.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/auth")]
[Produces("application/json")]
[Tags("Authentication")]
public partial class AuthController(
    IUserRegistrationService userRegistrationService,
    IAuthenticationService authenticationService,
    IPasswordService passwordService,
    ITwoFactorService twoFactorService,
    IUserProfileService userProfileService,
    IUserAuthorizationService userAuthorizationService,
    ISessionService sessionService,
    IEmailService emailService,
    IPasskeyService passkeyService,
    IOAuthService oauthService,
    [FromKeyedServices("GOAT_CLOUD")] IPermissionService permissionService) : ControllerBase
{
    private const string SessionIdClaimType = "sid";

    private const int AccessTokenExpirationMinutes = 60;
    private const int RefreshTokenExpirationDays = 7;
}
