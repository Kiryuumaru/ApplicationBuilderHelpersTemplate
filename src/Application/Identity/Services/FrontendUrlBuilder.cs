using System.Text.Encodings.Web;
using Application.Identity.Interfaces;
using Microsoft.Extensions.Options;

namespace Application.Identity.Services;

/// <summary>
/// Options for configuring frontend URLs.
/// </summary>
public sealed class FrontendUrlOptions
{
    /// <summary>
    /// The base URL for the frontend application (e.g., "https://example.com").
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The path for password reset page (default: "/reset-password").
    /// </summary>
    public string PasswordResetPath { get; set; } = "/reset-password";

    /// <summary>
    /// The path for email verification page (default: "/verify-email").
    /// </summary>
    public string EmailVerificationPath { get; set; } = "/verify-email";
}

/// <summary>
/// Builds frontend URLs using configured base URL and paths.
/// </summary>
internal sealed class FrontendUrlBuilder : IFrontendUrlBuilder
{
    private readonly FrontendUrlOptions _options;

    public FrontendUrlBuilder(IOptions<FrontendUrlOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string BuildPasswordResetUrl(string email, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var path = _options.PasswordResetPath.TrimStart('/');
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedToken = UrlEncoder.Default.Encode(token);

        return $"{baseUrl}/{path}?email={encodedEmail}&token={encodedToken}";
    }

    public string BuildEmailVerificationUrl(string email, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var path = _options.EmailVerificationPath.TrimStart('/');
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedToken = UrlEncoder.Default.Encode(token);

        return $"{baseUrl}/{path}?email={encodedEmail}&token={encodedToken}";
    }
}
