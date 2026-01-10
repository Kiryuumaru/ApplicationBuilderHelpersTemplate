namespace Application.Client.Authentication.Models;

/// <summary>
/// Result of a login operation.
/// </summary>
public sealed class LoginResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public int ExpiresIn { get; init; }
    public bool RequiresTwoFactor { get; init; }
    public string? TwoFactorToken { get; init; }

    public static LoginResult Succeeded(string accessToken, string refreshToken, int expiresIn) => new()
    {
        Success = true,
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        ExpiresIn = expiresIn
    };

    public static LoginResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };

    public static LoginResult TwoFactorRequired(string twoFactorToken) => new()
    {
        Success = false,
        RequiresTwoFactor = true,
        TwoFactorToken = twoFactorToken
    };
}
