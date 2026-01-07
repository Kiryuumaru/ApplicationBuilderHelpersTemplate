using Application.Identity.Interfaces;
using Application.Identity.Interfaces.Infrastructure;
using Domain.Identity.Exceptions;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;
using Domain.Shared.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace Application.Identity.Services;

/// <summary>
/// Implementation of IPasswordService using repositories directly.
/// </summary>
internal sealed class PasswordService(
    IUserRepository userRepository,
    IPasswordHasher<User> passwordHasher,
    IPasswordVerifier passwordVerifier,
    UserManager<User> userManager) : IPasswordService
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    private readonly IPasswordVerifier _passwordVerifier = passwordVerifier ?? throw new ArgumentNullException(nameof(passwordVerifier));
    private readonly UserManager<User> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new ValidationException("Password", "User does not have a password set.");
        }

        if (!_passwordVerifier.Verify(user.PasswordHash, currentPassword))
        {
            throw new InvalidPasswordException();
        }

        // Validate new password strength
        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, user, newPassword).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new PasswordValidationException($"Invalid password: {errors}");
            }
        }

        user.SetPasswordHash(_passwordHasher.HashPassword(user, newPassword));
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        // Validate new password strength
        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, user, newPassword).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new PasswordValidationException($"Invalid password: {errors}");
            }
        }

        user.SetPasswordHash(_passwordHasher.HashPassword(user, newPassword));
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task LinkPasswordAsync(Guid userId, string username, string password, string? email, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("User", userId.ToString());

        // SECURITY: Prevent overwriting existing password
        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new ValidationException("Password", "User already has a password linked. Use change password instead.");
        }

        // Check if username is already taken
        var existingUser = await _userRepository.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (existingUser is not null && existingUser.Id != userId)
        {
            throw new DuplicateEntityException("Username", username);
        }

        // Check if email is already taken
        if (!string.IsNullOrEmpty(email))
        {
            var existingByEmail = await _userRepository.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
            if (existingByEmail is not null && existingByEmail.Id != userId)
            {
                throw new DuplicateEntityException("Email", email);
            }
        }

        // Validate password strength
        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, user, password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new PasswordValidationException($"Invalid password: {errors}");
            }
        }

        // Upgrade from anonymous if needed
        if (user.IsAnonymous)
        {
            user.UpgradeFromAnonymous(username);
        }
        else
        {
            user.SetUserName(username);
            user.SetNormalizedUserName(username.ToUpperInvariant());
        }

        if (!string.IsNullOrEmpty(email))
        {
            user.SetEmail(email);
        }

        user.SetPasswordHash(_passwordHasher.HashPassword(user, password));
        await _userRepository.SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        // Generate reset token using ASP.NET Identity's built-in token provider
        var token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
        return token;
    }

    public async Task<bool> ResetPasswordWithTokenAsync(string email, string token, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        // Verify and reset password using ASP.NET Identity's built-in token verification
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword).ConfigureAwait(false);
        return result.Succeeded;
    }
}
