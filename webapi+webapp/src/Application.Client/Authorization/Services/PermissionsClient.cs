using System.Net.Http.Json;
using Application.Client.Authorization.Interfaces;
using Application.Client.Authorization.Models;
using Application.Client.Serialization;

namespace Application.Client.Authorization.Services;

/// <summary>
/// HTTP client for IAM permission endpoints.
/// </summary>
public class PermissionsClient : IPermissionsClient
{
    private readonly HttpClient _httpClient;

    public PermissionsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<IamPermission>> ListPermissionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/v1/iam/permissions", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    ApplicationClientJsonContext.Default.ListResponseIamPermission,
                    cancellationToken);
                return result?.Items ?? new List<IamPermission>();
            }

            return new List<IamPermission>();
        }
        catch
        {
            return new List<IamPermission>();
        }
    }

    public async Task<bool> GrantPermissionAsync(GrantPermissionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/iam/permissions/grant",
                request,
                ApplicationClientJsonContext.Default.GrantPermissionRequest,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RevokePermissionAsync(RevokePermissionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/iam/permissions/revoke",
                request,
                ApplicationClientJsonContext.Default.RevokePermissionRequest,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
