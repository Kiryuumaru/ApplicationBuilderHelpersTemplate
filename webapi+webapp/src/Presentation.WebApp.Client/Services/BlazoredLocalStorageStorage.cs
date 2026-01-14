using Application.Client.Authentication.Interfaces.Infrastructure;
using Application.Client.Authentication.Models;
using Application.LocalStore.Interfaces.Infrastructure;
using Blazored.LocalStorage;

namespace Presentation.WebApp.Client.Services;

/// <summary>
/// Token storage implementation using browser local storage.
/// </summary>
public class BlazoredLocalStorageStorage : ITokenStorage, ILocalStoreService
{
    private const string CredentialsKey = "auth_credentials";
    private readonly ILocalStorageService _localStorage;

    public BlazoredLocalStorageStorage(ILocalStorageService localStorage)
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
