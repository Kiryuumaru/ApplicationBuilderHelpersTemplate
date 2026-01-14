using Application.Client.Authentication.Models;

namespace Application.Client.Authentication.Interfaces.Infrastructure;

/// <summary>
/// Interface for storing and retrieving authentication tokens.
/// </summary>
public interface ITokenStorage
{
    /// <summary>
    /// Stores the credentials securely.
    /// </summary>
    Task StoreCredentialsAsync(StoredCredentials credentials);

    /// <summary>
    /// Retrieves stored credentials.
    /// </summary>
    Task<StoredCredentials?> GetCredentialsAsync();

    /// <summary>
    /// Clears all stored credentials.
    /// </summary>
    Task ClearCredentialsAsync();

    /// <summary>
    /// Checks if credentials are stored.
    /// </summary>
    Task<bool> HasCredentialsAsync();
}
