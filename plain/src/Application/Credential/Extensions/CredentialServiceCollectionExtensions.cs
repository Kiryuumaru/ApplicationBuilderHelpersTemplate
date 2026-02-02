using Application.Credential.Interfaces;
using Application.Credential.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Credential.Extensions;

internal static class CredentialServiceCollectionExtensions
{
    public static IServiceCollection AddCredentialServices(this IServiceCollection services)
    {
        services.AddScoped<ICredentialService, CredentialService>();

        return services;
    }
}
