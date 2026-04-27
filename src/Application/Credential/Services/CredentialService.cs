using Application.AppEnvironment.Interfaces.Inbound;
using Application.Credential.Extensions;
using Application.Credential.Interfaces.Inbound;
using Application.Credential.Models;
using Domain.Shared.Extensions;
using Microsoft.Extensions.Configuration;

namespace Application.Credential.Services;

internal sealed class CredentialService(IAppEnvironmentService appEnvironmentService, IConfiguration configuration) : ICredentialService
{
    public async Task<Models.Credentials> GetCredentials(CancellationToken cancellationToken)
    {
        var appEnv = await appEnvironmentService.GetEnvironment(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var sharedCredentials = configuration.GetCredentials("shared");
        var envCredentials = configuration.GetCredentials("environments", appEnv.Tag);
        return new Credentials
        {
            SharedCredentials = sharedCredentials,
            EnvironmentCredentials = envCredentials,
        };
    }
}
