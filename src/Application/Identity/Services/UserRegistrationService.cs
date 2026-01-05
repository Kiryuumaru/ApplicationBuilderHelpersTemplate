using Application.Authorization.Interfaces.Infrastructure;
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
    IPasswordHasher<User> passwordHasher,
    UserManager<User> userManager) : IUserRegistrationService
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IRoleRepository _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
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
                    ValidateRoleParameters(role, roleAssignment.ParameterValues);

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
        var roleCodes = await ResolveRoleCodesAsync(user, cancellationToken).ConfigureAwait(false);

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
        var roleCodes = await ResolveRoleCodesAsync(user, cancellationToken).ConfigureAwait(false);

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

    private async Task<IReadOnlyCollection<string>> ResolveRoleCodesAsync(User user, CancellationToken cancellationToken)
    {
        if (user.RoleAssignments.Count == 0)
        {
            return [];
        }

        var roleIds = user.RoleAssignments.Select(ra => ra.RoleId).Distinct();
        var roles = await _roleRepository.GetByIdsAsync(roleIds, cancellationToken).ConfigureAwait(false);

        return roles.Select(r => r.Code).ToArray();
    }

    private static void ValidateRoleParameters(Domain.Authorization.Models.Role role, IReadOnlyDictionary<string, string?>? providedParameters)
    {
        // Collect all required parameters from the role's scope templates
        var requiredParameters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scopeTemplate in role.ScopeTemplates)
        {
            foreach (var requiredParam in scopeTemplate.RequiredParameters)
            {
                requiredParameters.Add(requiredParam);
            }
        }

        if (requiredParameters.Count == 0)
        {
            return; // No parameters required
        }

        // Check that all required parameters are provided
        var providedKeys = providedParameters?.Keys ?? [];
        var missingParameters = requiredParameters.Where(p => !providedKeys.Contains(p)).ToList();

        if (missingParameters.Count > 0)
        {
            throw new ValidationException(
                $"Role '{role.Code}' requires parameters [{string.Join(", ", missingParameters)}] but they were not provided.");
        }
    }
}
