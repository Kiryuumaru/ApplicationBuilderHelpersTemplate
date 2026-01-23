using System.Net.Http.Json;
using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Models;
using Application.Client.Json;

namespace Application.Client.Authentication.Services;

internal class PasskeysClient : IPasskeysClient
{
    private readonly HttpClient _httpClient;

    public PasskeysClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(List<PasskeyInfo>? Passkeys, string? ErrorMessage)> ListPasskeysAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/auth/users/{userId}/identity/passkeys", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var listResponse = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.ListResponsePasskeyInfo,
                    cancellationToken);
                return (listResponse?.Items, null);
            }

            var error = await response.Content.ReadFromJsonAsync(
                AppJsonSerializerContext.Default.ErrorResponse,
                cancellationToken);
            return (null, error?.Detail ?? error?.Message ?? "Failed to list passkeys");
        }
        catch (Exception ex)
        {
            return (null, $"Network error: {ex.Message}");
        }
    }

    public async Task<(PasskeyRegistrationOptions? Options, string? ErrorMessage)> GetRegistrationOptionsAsync(Guid userId, string credentialName, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PasskeyRegistrationOptionsRequest
            {
                CredentialName = credentialName
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"api/v1/auth/users/{userId}/identity/passkeys/options",
                request,
                AppJsonSerializerContext.Default.PasskeyRegistrationOptionsRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var options = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.PasskeyRegistrationOptions,
                    cancellationToken);
                return (options, null);
            }

            var error = await response.Content.ReadFromJsonAsync(
                AppJsonSerializerContext.Default.ErrorResponse,
                cancellationToken);
            return (null, error?.Detail ?? error?.Message ?? "Failed to get registration options");
        }
        catch (Exception ex)
        {
            return (null, $"Network error: {ex.Message}");
        }
    }

    public async Task<(PasskeyRegistrationResult? Result, string? ErrorMessage)> RegisterPasskeyAsync(Guid userId, Guid challengeId, string attestationResponseJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PasskeyRegistrationRequest
            {
                ChallengeId = challengeId,
                AttestationResponseJson = attestationResponseJson
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"api/v1/auth/users/{userId}/identity/passkeys",
                request,
                AppJsonSerializerContext.Default.PasskeyRegistrationRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.PasskeyRegistrationResult,
                    cancellationToken);
                return (result, null);
            }

            var error = await response.Content.ReadFromJsonAsync(
                AppJsonSerializerContext.Default.ErrorResponse,
                cancellationToken);
            return (null, error?.Detail ?? error?.Message ?? "Failed to register passkey");
        }
        catch (Exception ex)
        {
            return (null, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> RenamePasskeyAsync(Guid userId, Guid credentialId, string newName, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PasskeyRenameRequest
            {
                Name = newName
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"api/v1/auth/users/{userId}/identity/passkeys/{credentialId}",
                request,
                AppJsonSerializerContext.Default.PasskeyRenameRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await response.Content.ReadFromJsonAsync(
                AppJsonSerializerContext.Default.ErrorResponse,
                cancellationToken);
            return (false, error?.Detail ?? error?.Message ?? "Failed to rename passkey");
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeletePasskeyAsync(Guid userId, Guid credentialId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"api/v1/auth/users/{userId}/identity/passkeys/{credentialId}",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await response.Content.ReadFromJsonAsync(
                AppJsonSerializerContext.Default.ErrorResponse,
                cancellationToken);
            return (false, error?.Detail ?? error?.Message ?? "Failed to delete passkey");
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }
}
