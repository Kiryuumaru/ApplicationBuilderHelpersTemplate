using System;
using Domain.Identity.Interfaces;
using Domain.Identity.Models;
using Microsoft.AspNetCore.Identity;

namespace Application.Identity.Services;

internal sealed class PasswordHasherVerifier(IPasswordHasher<User> passwordHasher) : IPasswordVerifier
{
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));

    public bool Verify(string passwordHash, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return false;
        }

        var result = _passwordHasher.VerifyHashedPassword(user: null!, passwordHash, providedPassword);
        return result != PasswordVerificationResult.Failed;
    }
}
