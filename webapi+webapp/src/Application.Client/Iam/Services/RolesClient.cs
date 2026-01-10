using System.Net.Http.Json;
using Application.Client.Iam.Interfaces;
using Application.Client.Iam.Models;
using Application.Client.Json;

namespace Application.Client.Iam.Services;

/// <summary>
/// HTTP client for IAM role endpoints.
/// </summary>
public class RolesClient : IRolesClient
{
    private readonly HttpClient _httpClient;

    public RolesClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<IamRole>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/v1/iam/roles", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.ListResponseIamRole,
                    cancellationToken);
                return result?.Items ?? new List<IamRole>();
            }

            return new List<IamRole>();
        }
        catch
        {
            return new List<IamRole>();
        }
    }

    public async Task<IamRole?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/iam/roles/{roleId}", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.IamRole,
                    cancellationToken);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IamRole?> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/iam/roles",
                request,
                AppJsonSerializerContext.Default.CreateRoleRequest,
                cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.IamRole,
                    cancellationToken);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IamRole?> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"api/v1/iam/roles/{roleId}",
                request,
                AppJsonSerializerContext.Default.UpdateRoleRequest,
                cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.IamRole,
                    cancellationToken);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/v1/iam/roles/{roleId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AssignRoleAsync(AssignRoleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/iam/roles/assign",
                request,
                AppJsonSerializerContext.Default.AssignRoleRequest,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UnassignRoleAsync(UnassignRoleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/iam/roles/remove",
                request,
                AppJsonSerializerContext.Default.UnassignRoleRequest,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
