using Application.Identity.Interfaces.Outbound;
using Domain.Identity.Models;
using Domain.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.Services;

internal sealed class AspNetIdentityPasswordHashService(IPasswordHasher<User> passwordHasher) : IPasswordHashService
{
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));

    public string Hash(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be null or whitespace.", nameof(password));
        }

        return _passwordHasher.HashPassword(user, password);
    }
}
