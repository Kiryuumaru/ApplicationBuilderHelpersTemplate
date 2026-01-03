using Domain.Identity.Models;
using Microsoft.AspNetCore.Identity;

namespace Application.Identity.Services;

/// <summary>
/// Custom user validator that allows null usernames for anonymous users.
/// </summary>
public class AnonymousUserValidator : IUserValidator<User>
{
    private readonly UserValidator<User> _defaultValidator = new();

    public async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user)
    {
        // Allow anonymous users to have null username
        if (user.IsAnonymous && string.IsNullOrEmpty(user.UserName))
        {
            return IdentityResult.Success;
        }

        // For non-anonymous users, use the default validation
        return await _defaultValidator.ValidateAsync(manager, user);
    }
}
