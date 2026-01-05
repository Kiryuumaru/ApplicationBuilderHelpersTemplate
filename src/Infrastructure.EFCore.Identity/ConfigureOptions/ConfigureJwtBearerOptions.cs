using Application.Authorization.Interfaces.Infrastructure;
using Application.Identity.Interfaces;
using ApplicationBuilderHelpers.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.EFCore.Identity.ConfigureOptions;

/// <summary>
/// Configures JWT Bearer authentication options for the application.
/// This lives in Infrastructure because it knows about JWT implementation details.
/// </summary>
public class ConfigureJwtBearerOptions(
    ILogger<ConfigureJwtBearerOptions> logger,
    IServiceProvider serviceProvider) : IConfigureNamedOptions<JwtBearerOptions>
{
    private const string SessionIdClaimType = "sid";
    private const string ServiceKey = "GOAT_CLOUD";

    public void Configure(string? name, JwtBearerOptions options)
    {
        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        using var scope = serviceProvider.CreateScope();
        var lifetimeService = scope.ServiceProvider.GetRequiredService<LifetimeService>();
        var jwtTokenService = scope.ServiceProvider.GetRequiredKeyedService<IJwtTokenService>(ServiceKey);

        options.TokenValidationParameters = jwtTokenService
            .GetTokenValidationParameters(lifetimeService.CreateCancellationToken())
            .GetAwaiter()
            .GetResult();

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                logger.LogWarning("JWT Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                logger.LogDebug("JWT Token validated successfully for user: {User}", context.Principal?.Identity?.Name ?? "Unknown");

                // Validate session is still active (not revoked)
                var sessionIdClaim = context.Principal?.FindFirst(SessionIdClaimType);
                if (sessionIdClaim is not null && Guid.TryParse(sessionIdClaim.Value, out var sessionId))
                {
                    using var validationScope = serviceProvider.CreateScope();
                    var sessionService = validationScope.ServiceProvider.GetRequiredService<ISessionService>();

                    var session = await sessionService.GetByIdAsync(sessionId, context.HttpContext.RequestAborted);
                    if (session is null || !session.IsValid)
                    {
                        logger.LogWarning("Session {SessionId} is revoked or invalid for user: {User}",
                            sessionId, context.Principal?.Identity?.Name ?? "Unknown");
                        context.Fail("Session has been revoked.");
                        return;
                    }
                }
            }
        };
    }
}
