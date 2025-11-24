using System;
using System.Security.Cryptography;
using Application.Identity.Interfaces;
using Domain.Identity.ValueObjects;

namespace Application.Identity.Services;

internal sealed class Pbkdf2PasswordCredentialFactory : IPasswordCredentialFactory
{
    private const string AlgorithmName = "pbkdf2-sha256";
    private const int DefaultIterations = 120_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public PasswordCredential Create(string secret, int? iterationOverride = null)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret cannot be null or empty.", nameof(secret));
        }

        var iterations = iterationOverride is { } overrideValue && overrideValue > 0
            ? overrideValue
            : DefaultIterations;
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, HashSize);

        return PasswordCredential.Create(
            algorithm: AlgorithmName,
            hash: Convert.ToBase64String(hash),
            salt: Convert.ToBase64String(salt),
            iterationCount: iterations);
    }
}
