using Application.Authorization.Extensions;
using Application.Identity.Interfaces;
using Application.Identity.Services;
using Domain.Authorization.Models;
using Domain.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Identity.Extensions;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRoleServices();

        services.AddIdentityCore<User>()
            .AddRoles<Role>()
            .AddSignInManager();

        services.AddScoped<IIdentityService, IdentityService>();
        return services;
    }
}
