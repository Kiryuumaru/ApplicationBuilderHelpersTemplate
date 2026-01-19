using Application.AppEnvironment.Services;
using Application.Authorization.Extensions;
using Application.Authorization.Models;
using Domain.Shared.Extensions;
using Microsoft.Extensions.Configuration;


namespace Application.Authorization.Services;

public class CredentialsService(AppEnvironmentService appEnvironmentService, IConfiguration configuration)
{
    public async Task<Credentials> GetCredentials(string envTag, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        var envCredentials = configuration.GetCredentials(envTag);
        return new Credentials
        {
            EnvironmentCredentials = envCredentials,
        };
    }

    public async Task<Credentials> GetCredentials(CancellationToken cancellationToken)
    {
        var appEnv = await appEnvironmentService.GetEnvironment(cancellationToken);
        return await GetCredentials(appEnv.Tag, cancellationToken);
    }
}
