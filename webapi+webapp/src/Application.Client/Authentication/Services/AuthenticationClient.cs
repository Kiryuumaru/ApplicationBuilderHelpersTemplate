using System.Net.Http.Json;
using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Models;
using Application.Client.Json;

namespace Application.Client.Authentication.Services;

/// <summary>
/// HTTP client for authentication API endpoints.
/// </summary>
public class AuthenticationClient : IAuthenticationClient
{
    private readonly HttpClient _httpClient;

    public AuthenticationClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LoginResult> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new LoginRequest
            {
                Username = usernameOrEmail,
                Password = password
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/auth/login",
                request,
                AppJsonSerializerContext.Default.LoginRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.TokenResponse,
                    cancellationToken);
                if (result != null)
                {
                    return LoginResult.Succeeded(result.AccessToken, result.RefreshToken, result.ExpiresIn);
                }
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var error = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.ErrorResponse,
                    cancellationToken);
                if (error?.RequiresTwoFactor == true)
                {
                    return LoginResult.TwoFactorRequired(error.TwoFactorToken ?? string.Empty);
                }
                return LoginResult.Failed(error?.Message ?? "Invalid credentials");
            }

            return LoginResult.Failed("Login failed");
        }
        catch (Exception ex)
        {
            return LoginResult.Failed($"Network error: {ex.Message}");
        }
    }

    public async Task<LoginResult> RegisterAsync(string email, string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new RegisterRequest
            {
                Email = email,
                Username = username,
                Password = password,
                ConfirmPassword = password
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/auth/register",
                request,
                AppJsonSerializerContext.Default.RegisterRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.TokenResponse,
                    cancellationToken);
                if (result != null)
                {
                    return LoginResult.Succeeded(result.AccessToken, result.RefreshToken, result.ExpiresIn);
                }
            }

            var error = await response.Content.ReadFromJsonAsync(
                AppJsonSerializerContext.Default.ErrorResponse,
                cancellationToken);
            return LoginResult.Failed(error?.Message ?? "Registration failed");
        }
        catch (Exception ex)
        {
            return LoginResult.Failed($"Network error: {ex.Message}");
        }
    }

    public async Task<LoginResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new RefreshTokenRequest
            {
                RefreshToken = refreshToken
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/auth/refresh",
                request,
                AppJsonSerializerContext.Default.RefreshTokenRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.TokenResponse,
                    cancellationToken);
                if (result != null)
                {
                    return LoginResult.Succeeded(result.AccessToken, result.RefreshToken, result.ExpiresIn);
                }
            }

            return LoginResult.Failed("Token refresh failed");
        }
        catch (Exception ex)
        {
            return LoginResult.Failed($"Network error: {ex.Message}");
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _httpClient.PostAsync("api/v1/auth/logout", null, cancellationToken);
        }
        catch
        {
            // Ignore logout errors - user is logging out anyway
        }
    }

    public async Task<bool> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ForgotPasswordRequest
            {
                Email = email
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/auth/forgot-password",
                request,
                AppJsonSerializerContext.Default.ForgotPasswordRequest,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ResetPasswordRequest
            {
                Email = email,
                Token = token,
                NewPassword = newPassword
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/auth/reset-password",
                request,
                AppJsonSerializerContext.Default.ResetPasswordRequest,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<LoginResult> ConfirmTwoFactorAsync(string code, string twoFactorToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new TwoFactorVerifyRequest
            {
                Code = code,
                TwoFactorToken = twoFactorToken
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/auth/2fa/verify",
                request,
                AppJsonSerializerContext.Default.TwoFactorVerifyRequest,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    AppJsonSerializerContext.Default.TokenResponse,
                    cancellationToken);
                if (result != null)
                {
                    return LoginResult.Succeeded(result.AccessToken, result.RefreshToken, result.ExpiresIn);
                }
            }

            return LoginResult.Failed("Two-factor verification failed");
        }
        catch (Exception ex)
        {
            return LoginResult.Failed($"Network error: {ex.Message}");
        }
    }
}
