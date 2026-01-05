using Infrastructure.Identity.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity.ConfigureOptions;

/// <summary>
/// Configures JWT Bearer authentication options using the IJwtTokenService.
/// </summary>
internal class ConfigureJwtBearerOptions(IServiceProvider serviceProvider) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options)
    {
        Configure(JwtBearerDefaults.AuthenticationScheme, options);
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        options.Events ??= new JwtBearerEvents();

        var originalOnMessageReceived = options.Events.OnMessageReceived;
        options.Events.OnMessageReceived = async context =>
        {
            if (originalOnMessageReceived is not null)
            {
                await originalOnMessageReceived(context);
            }

            var accessToken = context.Request.Query["access_token"];

            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
        };

        options.TokenValidationParameters = serviceProvider
            .GetRequiredService<IJwtTokenService>()
            .GetTokenValidationParameters(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
