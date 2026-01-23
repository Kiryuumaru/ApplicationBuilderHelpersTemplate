using System.Text.Json;
using Application.Client.Authentication.Interfaces.Infrastructure;
using Application.Client.Authentication.Models;
using Application.Client.Json;
using Application.LocalStore.Interfaces.Infrastructure;

namespace Application.Client.Authentication.Services;

/// <summary>
/// Token storage using ILocalStoreService (IndexedDB in browser).
/// </summary>
internal sealed class LocalStoreTokenStorage : ITokenStorage
{
    private const string GroupName = "auth";
    private const string CredentialsId = "credentials";

    private readonly ILocalStoreService _localStoreService;

    public LocalStoreTokenStorage(ILocalStoreService localStoreService)
    {
        _localStoreService = localStoreService;
    }

    public async Task StoreCredentialsAsync(StoredCredentials credentials)
    {
        var json = JsonSerializer.Serialize(credentials, AppJsonSerializerContext.Default.StoredCredentials);
        await _localStoreService.Open(CancellationToken.None);
        await _localStoreService.Set(GroupName, CredentialsId, json, CancellationToken.None);
        await _localStoreService.CommitAsync(CancellationToken.None);
    }

    public async Task<StoredCredentials?> GetCredentialsAsync()
    {
        try
        {
            await _localStoreService.Open(CancellationToken.None);
            var json = await _localStoreService.Get(GroupName, CredentialsId, CancellationToken.None);
            
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.StoredCredentials);
        }
        catch
        {
            return null;
        }
    }

    public async Task ClearCredentialsAsync()
    {
        await _localStoreService.Open(CancellationToken.None);
        await _localStoreService.Set(GroupName, CredentialsId, null, CancellationToken.None);
        await _localStoreService.CommitAsync(CancellationToken.None);
    }

    public async Task<bool> HasCredentialsAsync()
    {
        try
        {
            await _localStoreService.Open(CancellationToken.None);
            return await _localStoreService.Contains(GroupName, CredentialsId, CancellationToken.None);
        }
        catch
        {
            return false;
        }
    }
}
