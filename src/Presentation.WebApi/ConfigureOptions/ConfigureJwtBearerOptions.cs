using Application.Authorization.Interfaces;
using Application.Identity.Interfaces;
using ApplicationBuilderHelpers.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Presentation.WebApi.ConfigureOptions;

internal class ConfigureJwtBearerOptions(ILogger<ConfigureJwtBearerOptions> logger, IServiceProvider serviceProvider) : IConfigureNamedOptions<JwtBearerOptions>
{
    private const string SessionIdClaimType = "sid";

    public void Configure(string? name, JwtBearerOptions options)
    {
        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        using var scope = serviceProvider.CreateScope();
        var lifetimeService = scope.ServiceProvider.GetRequiredService<LifetimeService>();
        var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();

        options.TokenValidationParameters = permissionService.GetTokenValidationParametersAsync(lifetimeService.CreateCancellationToken()).Result;

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
