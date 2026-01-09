using Application.Identity.Interfaces.Infrastructure;
using Domain.Identity.Models;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.Services;

internal sealed class AspNetIdentityPasswordStrengthValidator(UserManager<User> userManager) : IPasswordStrengthValidator
{
    private readonly UserManager<User> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));

    public async Task<IReadOnlyCollection<string>> ValidateAsync(User user, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(password))
        {
            return ["Password cannot be empty."];
        }

        cancellationToken.ThrowIfCancellationRequested();

        var errors = new List<string>();
        foreach (var validator in _userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_userManager, user, password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors.Select(static e => e.Description));
            }
        }

        return errors;
    }
}
