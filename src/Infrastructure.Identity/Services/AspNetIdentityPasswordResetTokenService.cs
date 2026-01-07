using Application.Identity.Interfaces.Infrastructure;
using Domain.Identity.Models;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.Services;

internal sealed class AspNetIdentityPasswordResetTokenService(UserManager<User> userManager) : IPasswordResetTokenService
{
    private readonly UserManager<User> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));

    public Task<string> GenerateResetTokenAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        cancellationToken.ThrowIfCancellationRequested();
        return _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<bool> ResetPasswordWithTokenAsync(User user, string token, string newPassword, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be null or whitespace.", nameof(token));
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new ArgumentException("New password cannot be null or whitespace.", nameof(newPassword));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword).ConfigureAwait(false);
        return result.Succeeded;
    }
}
