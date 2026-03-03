using System.Text.Encodings.Web;
using Application.Server.Identity.Interfaces.Inbound;
using Application.Server.Identity.Models;
using Microsoft.Extensions.Options;

namespace Application.Server.Identity.Services;

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
