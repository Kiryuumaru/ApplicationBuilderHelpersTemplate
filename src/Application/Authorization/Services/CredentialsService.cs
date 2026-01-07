using Application.AppEnvironment.Services;
using Application.Authorization.Extensions;
using Application.Authorization.Interfaces;
using Application.Authorization.Models;
using Domain.Shared.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Application.Authorization.Services;

public class CredentialsService(AppEnvironmentService appEnvironmentService, IConfiguration configuration)
{
    public async Task<Credentials> GetCredentials(string envTag, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        var envCredentials = configuration.GetCredentials(envTag);
        var jwtSecret = envCredentials.GetValueOrThrow<string>("jwt", "secret");
        var jwtIssuer = envCredentials.GetValueOrThrow<string>("jwt", "issuer");
        var jwtAudience = envCredentials.GetValueOrThrow<string>("jwt", "audience");
        var defaultExpirationSeconds = envCredentials.GetValueOrDefault<double?>(null, "jwt", "default_expiration_seconds");
        var defaultClockSkewSeconds = envCredentials.GetValueOrDefault<double?>(null, "jwt", "clock_skew_seconds");

        TimeSpan defaultExpiration = TimeSpan.FromHours(1);
        if (defaultExpirationSeconds.HasValue)
        {
            var expirationSeconds = Math.Max(0, defaultExpirationSeconds.Value);
            defaultExpiration = TimeSpan.FromSeconds(expirationSeconds);
        }

        TimeSpan clockSkew = TimeSpan.FromMinutes(5);
        if (defaultClockSkewSeconds.HasValue)
        {
            var clockSkewSeconds = Math.Max(0, defaultClockSkewSeconds.Value);
            clockSkew = TimeSpan.FromSeconds(clockSkewSeconds);
        }
        return new Credentials
        {
            JwtConfiguration = new JwtConfiguration
            {
                Secret = jwtSecret,
                Issuer = jwtIssuer,
                Audience = jwtAudience,
                ClockSkew = clockSkew,
                DefaultExpiration = defaultExpiration,
            }
        };
    }

    public async Task<Credentials> GetCredentials(CancellationToken cancellationToken)
    {
        var appEnv = await appEnvironmentService.GetEnvironment(cancellationToken);
        return await GetCredentials(appEnv.Tag, cancellationToken);
    }
}
