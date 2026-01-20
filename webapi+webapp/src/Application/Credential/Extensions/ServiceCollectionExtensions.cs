using Application.Credential.Interfaces;
using Application.Credential.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Credential.Extensions;

internal static class ServiceCollectionExtensions
{
    public static void AddCredentialServices(this IServiceCollection services)
    {
        services.AddScoped<ICredentialService, CredentialService>();
    }
}
