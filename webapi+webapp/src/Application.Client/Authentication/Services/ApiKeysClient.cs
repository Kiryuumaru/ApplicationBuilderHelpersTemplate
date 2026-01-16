using System.Net.Http.Json;
using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Models;
using Application.Client.Json;

namespace Application.Client.Authentication.Services;

internal class ApiKeysClient : IApiKeysClient
{
    private readonly HttpClient _httpClient;

    public ApiKeysClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ApiKeyInfo>> ListApiKeysAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/auth/users/{userId}/api-keys", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.ListResponseApiKeyInfo,
                    cancellationToken);
                return result?.Items ?? new List<ApiKeyInfo>();
            }

            return new List<ApiKeyInfo>();
        }
        catch
        {
            return new List<ApiKeyInfo>();
        }
    }

    public async Task<CreateApiKeyResult?> CreateApiKeyAsync(Guid userId, string name, DateTimeOffset? expiresAt = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CreateApiKeyRequest
            {
                Name = name,
                ExpiresAt = expiresAt
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"api/v1/auth/users/{userId}/api-keys",
                request,
                AppJsonSerializerContext.Default.CreateApiKeyRequest,
                cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.CreateApiKeyResult,
                    cancellationToken);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RevokeApiKeyAsync(Guid userId, Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/v1/auth/users/{userId}/api-keys/{apiKeyId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
