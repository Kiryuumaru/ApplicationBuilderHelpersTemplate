using Application.AppEnvironment.Services;
using Application.Server.Authorization.Extensions;
using Application.Server.Authorization.Interfaces;
using Application.Server.Authorization.Models;
using Domain.Shared.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Application.Server.Authorization.Services;

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
