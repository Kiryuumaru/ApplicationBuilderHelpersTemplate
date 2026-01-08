using Application.Authorization.Interfaces;
using Application.Identity.Interfaces;
using Application.Identity.Models;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Controllers.V1.Iam.UsersController.Requests;
using Presentation.WebApi.Controllers.V1.Iam.UsersController.Responses;
using Presentation.WebApi.Models.Shared;

namespace Presentation.WebApi.Controllers.V1.Iam.UsersController;

/// <summary>
/// IAM user operations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/iam")]
[Produces("application/json")]
[Authorize]
[Tags("IAM")]
public sealed class IamUsersController(
    IUserProfileService userProfileService,
    IUserAuthorizationService userAuthorizationService,
    IUserRegistrationService userRegistrationService,
    IPasswordService passwordService) : ControllerBase
{
    /// <summary>
    /// Lists all users (admin only).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all users.</returns>
    [HttpGet("users")]
    [RequiredPermission(PermissionIds.Api.Iam.Users.List.Identifier)]
    [ProducesResponseType<PagedResponse<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListUsers(CancellationToken cancellationToken)
    {
        var users = await userProfileService.ListAsync(cancellationToken);

        var items = users.Select(MapToResponse).ToList();

        return Ok(PagedResponse<UserResponse>.From(items, items.Count));
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user details.</returns>
    [HttpGet("users/{id:guid}")]
    [RequiredPermission(PermissionIds.Api.Iam.Users.ReadPermission.Identifier)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(
        [PermissionParameter(PermissionIds.Api.Iam.Users.ReadPermission.UserIdParameter)] Guid id,
        CancellationToken cancellationToken)
    {
        var user = await userProfileService.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            throw new EntityNotFoundException("User", id.ToString());
        }

        return Ok(MapToResponse(user));
    }

    /// <summary>
    /// Updates a user's profile.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user.</returns>
    [HttpPut("users/{id:guid}")]
    [RequiredPermission(PermissionIds.Api.Iam.Users.Update.Identifier)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateUser(
        [PermissionParameter(PermissionIds.Api.Iam.Users.Update.UserIdParameter)] Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        await userProfileService.UpdateUserAsync(id, new UserUpdateRequest
        {
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            LockoutEnabled = request.LockoutEnabled
        }, cancellationToken);

        var user = await userProfileService.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            throw new EntityNotFoundException("User", id.ToString());
        }

        return Ok(MapToResponse(user));
    }

    /// <summary>
    /// Deletes a user (admin only).
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpDelete("users/{id:guid}")]
    [RequiredPermission(PermissionIds.Api.Iam.Users.Delete.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await userRegistrationService.DeleteUserAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Gets the effective permissions for a user.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The effective permissions.</returns>
    [HttpGet("users/{id:guid}/permissions")]
    [RequiredPermission(PermissionIds.Api.Iam.Users.Permissions.Identifier)]
    [ProducesResponseType<PermissionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPermissions(
        [PermissionParameter(PermissionIds.Api.Iam.Users.Permissions.UserIdParameter)] Guid id,
        CancellationToken cancellationToken)
    {
        var permissions = await userAuthorizationService.GetEffectivePermissionsAsync(id, cancellationToken);

        return Ok(new PermissionsResponse
        {
            UserId = id,
            Permissions = permissions
        });
    }

    /// <summary>
    /// Resets a user's password (admin only).
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="request">The password reset request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPut("users/{id:guid}/password")]
    [RequiredPermission(PermissionIds.Api.Iam.Users.ResetPassword.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] AdminResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await passwordService.ResetPasswordAsync(id, request.NewPassword, cancellationToken);
        return NoContent();
    }

    private static UserResponse MapToResponse(UserDto user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd,
            AccessFailedCount = user.AccessFailedCount,
            Created = user.Created,
            RoleIds = user.RoleIds.ToList()
        };
    }
}
