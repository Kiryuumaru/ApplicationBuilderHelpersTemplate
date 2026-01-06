using Application.Authorization.Interfaces;
using Application.Authorization.Interfaces.Infrastructure;
using Application.Authorization.Services;
using Application.Identity.Interfaces;
using Application.Identity.Interfaces.Infrastructure;
using Application.Identity.Models;
using Domain.Identity.Exceptions;
using Domain.Identity.Models;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace Application.Identity.Services;

/// <summary>
/// Implementation of IUserRegistrationService using repositories directly.
/// </summary>
internal sealed class UserRegistrationService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUserRoleResolver userRoleResolver,
    IPasswordHasher<User> passwordHasher,
    UserManager<User> userManager) : IUserRegistrationService
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IRoleRepository _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
    private readonly IUserRoleResolver _userRoleResolver = userRoleResolver ?? throw new ArgumentNullException(nameof(userRoleResolver));
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    private readonly UserManager<User> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));

    public async Task<UserDto> RegisterUserAsync(UserRegistrationRequest? request, CancellationToken cancellationToken)
    {
        User user;

        if (request is null)
        {
            // Create anonymous user
            user = User.RegisterAnonymous();
        }
        else
        {
            // Validate password confirmation
            if (!request.PasswordsMatch())
            {
                throw new PasswordValidationException("Password and confirm password do not match.");
            }

            // Validate uniqueness
            var existingByUsername = await _userRepository.FindByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
            if (existingByUsername is not null)
            {
                throw new DuplicateEntityException("Username", request.Username);
            }

            if (!string.IsNullOrEmpty(request.Email))
            {
                var existingByEmail = await _userRepository.FindByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false);
                if (existingByEmail is not null)
                {
                    throw new DuplicateEntityException("Email", request.Email);
                }
            }

            // Validate password strength
            user = User.Register(request.Username, request.Email);
            var passwordValidators = _userManager.PasswordValidators;
            foreach (var validator in passwordValidators)
            {
                var passwordValidationResult = await validator.ValidateAsync(_userManager, user, request.Password).ConfigureAwait(false);
                if (!passwordValidationResult.Succeeded)
                {
                    var errors = string.Join("; ", passwordValidationResult.Errors.Select(e => e.Description));
                    throw new PasswordValidationException($"Invalid password: {errors}");
                }
            }

            user.SetPasswordHash(_passwordHasher.HashPassword(user, request.Password));

            // Process additional role assignments from the request
            if (request.RoleAssignments is { Count: > 0 })
            {
                foreach (var roleAssignment in request.RoleAssignments)
                {
                    var role = await _roleRepository.GetByCodeAsync(roleAssignment.RoleCode, cancellationToken).ConfigureAwait(false)
                        ?? throw new EntityNotFoundException("Role", roleAssignment.RoleCode);

                    // Validate required parameters
                    RoleValidationHelper.ValidateRoleParameters(role, roleAssignment.ParameterValues);

                    user.AssignRole(role.Id, roleAssignment.ParameterValues);
                }
            }

            // Process permission grants from the request
            if (request.PermissionIdentifiers is { Count: > 0 })
            {
                foreach (var permissionIdentifier in request.PermissionIdentifiers)
                {
                    var grant = Domain.Identity.ValueObjects.UserPermissionGrant.Allow(
                        permissionIdentifier,
                        description: "Granted during registration");
                    user.GrantPermission(grant);
                }
            }
        }

        // Assign default User role with userId parameter
        var userRoleId = Domain.Authorization.Constants.Roles.User.Id;
        user.AssignRole(userRoleId, new Dictionary<string, string?> { ["roleUserId"] = user.Id.ToString() });

        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);

        var externalLogins = await _userRepository.GetLoginsAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var roleResolutions = await _userRoleResolver.ResolveRolesAsync(user, cancellationToken).ConfigureAwait(false);
        var roleCodes = roleResolutions.Select(r => r.Code).ToArray();

        return user.ToDto(user.RoleIds, roleCodes, externalLogins);
    }

    public async Task<UserDto> RegisterExternalAsync(ExternalUserRegistrationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check if this external login already exists
        var existingUserId = await _userRepository.FindUserByLoginAsync(request.Provider, request.ProviderSubject, cancellationToken).ConfigureAwait(false);
        if (existingUserId.HasValue)
        {
            throw new DuplicateEntityException("ExternalLogin", $"{request.Provider}:{request.ProviderSubject}");
        }

        var user = User.RegisterExternal(
            request.Username,
            request.Provider.ToString(),
            request.ProviderSubject,
            request.ProviderEmail,
            request.ProviderDisplayName,
            request.Email);

        // Assign default User role with userId parameter
        var userRoleId = Domain.Authorization.Constants.Roles.User.Id;
        user.AssignRole(userRoleId, new Dictionary<string, string?> { ["roleUserId"] = user.Id.ToString() });

        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);

        // Add external login
        await _userRepository.AddLoginAsync(
            user.Id,
            request.Provider,
            request.ProviderSubject,
            request.ProviderDisplayName,
            request.ProviderEmail,
            cancellationToken).ConfigureAwait(false);

        var externalLogins = await _userRepository.GetLoginsAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var roleResolutions = await _userRoleResolver.ResolveRolesAsync(user, cancellationToken).ConfigureAwait(false);
        var roleCodes = roleResolutions.Select(r => r.Code).ToArray();

        return user.ToDto(user.RoleIds, roleCodes, externalLogins);
    }

    public async Task UpgradeAnonymousWithPasskeyAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        if (!user.IsAnonymous)
        {
            throw new ValidationException("userId", "User is not anonymous and cannot be upgraded.");
        }

        // For passkey upgrade, we don't need a username - just mark as non-anonymous
        user.UpgradeFromAnonymousWithPasskey();
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        await _userRepository.DeleteAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
