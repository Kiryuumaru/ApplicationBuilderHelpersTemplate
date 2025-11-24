using System;
using System.Security.Cryptography;
using Domain.Identity.Interfaces;
using Domain.Identity.ValueObjects;

namespace Application.Identity.Services;

internal sealed class Pbkdf2UserSecretValidator : IUserSecretValidator
{
    private const string SupportedAlgorithm = "pbkdf2-sha256";

    public bool Verify(PasswordCredential credential, ReadOnlySpan<char> secret)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (!string.Equals(credential.Algorithm, SupportedAlgorithm, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (secret.IsEmpty)
        {
            return false;
        }

        var salt = Convert.FromBase64String(credential.Salt);
        var expectedHash = Convert.FromBase64String(credential.Hash);
        var derived = Rfc2898DeriveBytes.Pbkdf2(secret.ToString(), salt, credential.IterationCount, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(derived, expectedHash);
    }
}
