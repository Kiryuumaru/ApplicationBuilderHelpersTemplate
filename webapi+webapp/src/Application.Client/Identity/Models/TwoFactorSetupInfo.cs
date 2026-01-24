namespace Application.Client.Identity.Models;

/// <summary>
/// Represents 2FA setup information including the shared key and QR code URI.
/// </summary>
public class TwoFactorSetupInfo
{
    /// <summary>
    /// Gets or sets the shared key for manual entry.
    /// </summary>
    public string SharedKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the formatted shared key (e.g., XXXX-XXXX-XXXX-XXXX).
    /// </summary>
    public string FormattedSharedKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authenticator URI for QR code generation.
    /// </summary>
    public string AuthenticatorUri { get; set; } = string.Empty;
}

/// <summary>
/// Represents the result of enabling 2FA.
/// </summary>
public class EnableTwoFactorResult
{
    /// <summary>
    /// Gets or sets whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the recovery codes (returned when 2FA is enabled).
    /// </summary>
    public List<string> RecoveryCodes { get; set; } = new();

    public static EnableTwoFactorResult Succeeded(List<string> recoveryCodes) => new()
    {
        Success = true,
        RecoveryCodes = recoveryCodes
    };

    public static EnableTwoFactorResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
