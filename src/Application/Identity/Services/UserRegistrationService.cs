using Application.Authorization.Interfaces.Infrastructure;
using Application.Identity.Interfaces;
using Application.Identity.Interfaces.Infrastructure;
using Application.Identity.Models;
using Domain.Identity.Models;
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
                throw new InvalidOperationException($"Username '{request.Username}' is already taken.");
            }

            if (!string.IsNullOrEmpty(request.Email))
            {
                var existingByEmail = await _userRepository.FindByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false);
                if (existingByEmail is not null)
                {
                    throw new InvalidOperationException($"Email '{request.Email}' is already registered.");
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
                    throw new InvalidOperationException($"Invalid password: {errors}");
                }
            }

            user.SetPasswordHash(_passwordHasher.HashPassword(user, request.Password));
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
            throw new InvalidOperationException($"External login already linked to another account.");
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
            ?? throw new InvalidOperationException($"User with ID {userId} not found.");

        if (!user.IsAnonymous)
        {
            throw new InvalidOperationException("User is not anonymous and cannot be upgraded.");
        }

        // For passkey upgrade, we don't need a username - just mark as non-anonymous
        user.UpgradeFromAnonymousWithPasskey();
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User with ID {userId} not found.");

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
}
