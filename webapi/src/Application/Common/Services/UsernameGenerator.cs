namespace Application.Common.Services;

/// <summary>
/// Generates usernames for various registration flows.
/// </summary>
public static class UsernameGenerator
{
    /// <summary>
    /// Generates a unique username from OAuth provider information.
    /// </summary>
    /// <param name="name">The user's display name from the provider.</param>
    /// <param name="email">The user's email from the provider.</param>
    /// <param name="providerSubject">The provider's unique subject identifier.</param>
    /// <returns>A unique username string.</returns>
    public static string FromOAuth(string? name, string? email, string providerSubject)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerSubject);

        var baseUsername = name?.Replace(" ", "").ToLowerInvariant()
            ?? email?.Split('@')[0].ToLowerInvariant()
            ?? $"user_{providerSubject[..Math.Min(8, providerSubject.Length)]}";

        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{baseUsername}_{suffix}";
    }
}
