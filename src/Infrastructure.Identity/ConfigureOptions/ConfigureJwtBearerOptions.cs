using Application.Identity.Interfaces;
using Domain.Identity.Constants;
using Infrastructure.Identity.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using JwtClaimTypes = Domain.Identity.Constants.JwtClaimTypes;

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

        // Add session validation on token validated
        var originalOnTokenValidated = options.Events.OnTokenValidated;
        options.Events.OnTokenValidated = async context =>
        {
            if (originalOnTokenValidated is not null)
            {
                await originalOnTokenValidated(context);
            }

            // Skip if authentication already failed
            if (context.Result?.Failure is not null)
            {
                return;
            }

            // Extract session ID from claims
            var sessionIdClaim = context.Principal?.FindFirst(JwtClaimTypes.SessionId);
            if (sessionIdClaim is null || !Guid.TryParse(sessionIdClaim.Value, out var sessionId))
            {
                context.Fail("Token is missing required session identifier.");
                return;
            }

            // Validate session is still active
            using var scope = context.HttpContext.RequestServices.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
            var session = await sessionService.GetByIdAsync(sessionId, context.HttpContext.RequestAborted);

            if (session is null || !session.IsValid)
            {
                context.Fail("Session has been revoked or is no longer valid.");
            }
        };

        // Create a scope to resolve scoped services (e.g., CredentialsService used by the JWT config factory)
        using var scope = serviceProvider.CreateScope();
        options.TokenValidationParameters = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>()
            .GetTokenValidationParameters(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        // Disable claim type mapping - keep JWT claim types as-is ("sub" stays "sub", not mapped to ClaimTypes.NameIdentifier)
        options.MapInboundClaims = false;
    }
}
