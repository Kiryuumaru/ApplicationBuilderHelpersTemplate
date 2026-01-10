using System.Net.Http.Json;
using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Models;
using Application.Client.Json;

namespace Application.Client.Authentication.Services;

/// <summary>
/// HTTP client for user profile endpoints.
/// </summary>
public class UserProfileClient : IUserProfileClient
{
    private readonly HttpClient _httpClient;

    public UserProfileClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UserProfile?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/v1/auth/me", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.UserProfile,
                    cancellationToken);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ChangePasswordRequest
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"api/v1/auth/users/{userId}/identity/password",
                request,
                AppJsonSerializerContext.Default.ChangePasswordRequest,
                cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await response.Content.ReadFromJsonAsync(
                AppJsonSerializerContext.Default.ErrorResponse,
                cancellationToken);
            return (false, error?.Detail ?? error?.Message ?? "Failed to change password");
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }
}
