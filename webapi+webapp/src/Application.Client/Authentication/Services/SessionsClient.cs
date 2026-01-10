using System.Net.Http.Json;
using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Models;
using Application.Client.Json;

namespace Application.Client.Authentication.Services;

/// <summary>
/// HTTP client for session management API endpoints.
/// </summary>
public class SessionsClient : ISessionsClient
{
    private readonly HttpClient _httpClient;

    public SessionsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<SessionInfo>> ListSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/auth/users/{userId}/sessions", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.ListResponseSessionInfo,
                    cancellationToken);
                return result?.Items ?? new List<SessionInfo>();
            }

            return new List<SessionInfo>();
        }
        catch
        {
            return new List<SessionInfo>();
        }
    }

    public async Task<bool> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/v1/auth/users/{userId}/sessions/{sessionId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/v1/auth/users/{userId}/sessions", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.RevokeAllResponse,
                    cancellationToken);
                return result?.RevokedCount ?? 0;
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }
}
