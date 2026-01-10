using Application.Client.Authentication.Models;

namespace Application.Client.Authentication.Interfaces;

/// <summary>
/// Provides access to the current authentication state.
/// </summary>
public interface IAuthStateProvider
{
    /// <summary>
    /// Gets the current authentication state.
    /// </summary>
    AuthState CurrentState { get; }

    /// <summary>
    /// Event raised when authentication state changes.
    /// </summary>
    event Action? OnStateChanged;

    /// <summary>
    /// Updates the authentication state with new credentials.
    /// </summary>
    Task UpdateStateAsync(StoredCredentials credentials);

    /// <summary>
    /// Clears the current authentication state (logout).
    /// </summary>
    Task ClearStateAsync();

    /// <summary>
    /// Initializes the authentication state from stored credentials.
    /// </summary>
    Task InitializeAsync();
}
