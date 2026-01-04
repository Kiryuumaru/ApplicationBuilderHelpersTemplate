using Application.Identity.Interfaces;
using Application.Identity.Models;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Domain.Identity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.WebApi.Attributes;
using Presentation.WebApi.Models.Requests;
using Presentation.WebApi.Models.Responses;

namespace Presentation.WebApi.Controllers.V1.Iam;

/// <summary>
/// Controller for user management operations within IAM (Identity and Access Management).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{v:apiVersion}/iam/users")]
[Produces("application/json")]
[Tags("IAM - Users")]
[Authorize]
public class UsersController(
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
    [HttpGet]
    [RequiredPermission(PermissionIds.Api.Iam.Users.List.Identifier)]
    [ProducesResponseType<UserListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListUsers(CancellationToken cancellationToken)
    {
        var users = await userProfileService.ListAsync(cancellationToken);

        var response = new UserListResponse
        {
            Users = users.Select(MapToResponse).ToList(),
            Total = users.Count
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user details.</returns>
    [HttpGet("{id:guid}")]
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
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found",
                Detail = $"No user found with ID '{id}'."
            });
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
    [HttpPut("{id:guid}")]
    [RequiredPermission(PermissionIds.Api.Iam.Users.Update.Identifier)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateUser(
        [PermissionParameter(PermissionIds.Api.Iam.Users.Update.UserIdParameter)] Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await userProfileService.UpdateUserAsync(id, new UserUpdateRequest
            {
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                LockoutEnabled = request.LockoutEnabled
            }, cancellationToken);

            var user = await userProfileService.GetByIdAsync(id, cancellationToken);
            return Ok(MapToResponse(user!));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found",
                Detail = $"No user found with ID '{id}'."
            });
        }
    }

    /// <summary>
    /// Deletes a user (admin only).
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{id:guid}")]
    [RequiredPermission(PermissionIds.Api.Iam.Users.Delete.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await userRegistrationService.DeleteUserAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found",
                Detail = $"No user found with ID '{id}'."
            });
        }
    }

    /// <summary>
    /// Gets the effective permissions for a user.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The effective permissions.</returns>
    [HttpGet("{id:guid}/permissions")]
    [RequiredPermission(PermissionIds.Api.Iam.Users.Permissions.Identifier)]
    [ProducesResponseType<PermissionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPermissions(
        [PermissionParameter(PermissionIds.Api.Iam.Users.Permissions.UserIdParameter)] Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var permissions = await userAuthorizationService.GetEffectivePermissionsAsync(id, cancellationToken);

            return Ok(new PermissionsResponse
            {
                UserId = id,
                Permissions = permissions
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found",
                Detail = $"No user found with ID '{id}'."
            });
        }
    }

    /// <summary>
    /// Resets a user's password (admin only).
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="request">The password reset request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPut("{id:guid}/password")]
    [RequiredPermission(PermissionIds.Api.Iam.Users.ResetPassword.Identifier)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] AdminResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await passwordService.ResetPasswordAsync(id, request.NewPassword, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found",
                Detail = $"No user found with ID '{id}'."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Password reset failed",
                Detail = ex.Message
            });
        }
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
