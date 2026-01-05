using Application.Authorization.Interfaces.Infrastructure;
using Application.Identity.Interfaces;
using Application.Identity.Interfaces.Infrastructure;
using Application.Identity.Models;
using Domain.Identity.Models;
using Domain.Shared.Exceptions;

namespace Application.Identity.Services;

/// <summary>
/// Implementation of IUserProfileService using repositories directly.
/// </summary>
internal sealed class UserProfileService(
    IUserRepository userRepository,
    IRoleRepository roleRepository) : IUserProfileService
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IRoleRepository _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        return await MapToUserDtoAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        return await MapToUserDtoAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserDto?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        return await MapToUserDtoAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<UserDto>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await _userRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<UserDto>(users.Count);

        foreach (var user in users)
        {
            var dto = await MapToUserDtoAsync(user, cancellationToken).ConfigureAwait(false);
            result.Add(dto);
        }

        return result;
    }

    public async Task UpdateUserAsync(Guid userId, UserUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        // Apply updates
        if (request.Email is not null)
        {
            user.SetEmail(request.Email);
            user.SetNormalizedEmail(request.Email.ToUpperInvariant());
        }

        if (request.PhoneNumber is not null)
        {
            user.SetPhoneNumber(request.PhoneNumber);
        }

        if (request.LockoutEnabled.HasValue)
        {
            user.SetLockoutEnabled(request.LockoutEnabled.Value);
        }

        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task ChangeUsernameAsync(Guid userId, string newUsername, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        // Check if username is already taken
        var existing = await _userRepository.FindByUsernameAsync(newUsername, cancellationToken).ConfigureAwait(false);
        if (existing is not null && existing.Id != userId)
        {
            throw new DuplicateEntityException("Username", newUsername);
        }

        user.SetUserName(newUsername);
        user.SetNormalizedUserName(newUsername.ToUpperInvariant());
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task ChangeEmailAsync(Guid userId, string newEmail, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        // Check if email is already taken
        var existing = await _userRepository.FindByEmailAsync(newEmail, cancellationToken).ConfigureAwait(false);
        if (existing is not null && existing.Id != userId)
        {
            throw new DuplicateEntityException("Email", newEmail);
        }

        user.SetEmail(newEmail);
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task LinkEmailAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        // Check if email is already taken
        var existing = await _userRepository.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existing is not null && existing.Id != userId)
        {
            throw new DuplicateEntityException("Email", email);
        }

        user.SetEmail(email);
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task UnlinkEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        user.SetEmail(null);
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task UnlinkExternalLoginAsync(Guid userId, Domain.Identity.Enums.ExternalLoginProvider provider, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        await _userRepository.RemoveLoginAsync(userId, provider, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UserDto> MapToUserDtoAsync(User user, CancellationToken cancellationToken)
    {
        var externalLogins = await _userRepository.GetLoginsAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var roleCodes = await ResolveRoleCodesAsync(user, cancellationToken).ConfigureAwait(false);

        return user.ToDto(user.RoleIds, roleCodes, externalLogins);
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
