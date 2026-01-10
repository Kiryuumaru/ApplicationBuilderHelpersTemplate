using System.Net.Http.Json;
using Application.Client.Iam.Interfaces;
using Application.Client.Iam.Models;
using Application.Client.Json;

namespace Application.Client.Iam.Services;

/// <summary>
/// HTTP client for IAM user endpoints.
/// </summary>
public class UsersClient : IUsersClient
{
    private readonly HttpClient _httpClient;

    public UsersClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<IamUser>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/v1/iam/users", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.PagedResponseIamUser,
                    cancellationToken);
                return result?.Items ?? new List<IamUser>();
            }

            return new List<IamUser>();
        }
        catch
        {
            return new List<IamUser>();
        }
    }

    public async Task<IamUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/iam/users/{userId}", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.IamUser,
                    cancellationToken);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IamUser?> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"api/v1/iam/users/{userId}",
                request,
                AppJsonSerializerContext.Default.UpdateUserRequest,
                cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.IamUser,
                    cancellationToken);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/v1/iam/users/{userId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UserPermissions?> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/iam/users/{userId}/permissions", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.UserPermissions,
                    cancellationToken);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ResetUserPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ResetUserPasswordRequest
            {
                NewPassword = newPassword
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"api/v1/iam/users/{userId}/password",
                request,
                AppJsonSerializerContext.Default.ResetUserPasswordRequest,
                cancellationToken);
            
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
