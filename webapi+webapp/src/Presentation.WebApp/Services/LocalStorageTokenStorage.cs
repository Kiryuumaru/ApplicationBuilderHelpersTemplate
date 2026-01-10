using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Models;
using Blazored.LocalStorage;

namespace Presentation.WebApp.Services;

/// <summary>
/// Token storage implementation using browser local storage.
/// </summary>
public class LocalStorageTokenStorage : ITokenStorage
{
    private const string CredentialsKey = "auth_credentials";
    private readonly ILocalStorageService _localStorage;

    public LocalStorageTokenStorage(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task StoreCredentialsAsync(StoredCredentials credentials)
    {
        await _localStorage.SetItemAsync(CredentialsKey, credentials);
    }

    public async Task<StoredCredentials?> GetCredentialsAsync()
    {
        try
        {
            return await _localStorage.GetItemAsync<StoredCredentials>(CredentialsKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task ClearCredentialsAsync()
    {
        await _localStorage.RemoveItemAsync(CredentialsKey);
    }

    public async Task<bool> HasCredentialsAsync()
    {
        return await _localStorage.ContainKeyAsync(CredentialsKey);
    }
}
