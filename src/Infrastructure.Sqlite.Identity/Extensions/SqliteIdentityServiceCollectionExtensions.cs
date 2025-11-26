using Application.Authorization.Interfaces;
using Domain.Authorization.Models;
using Domain.Identity.Models;
using Infrastructure.Sqlite.Identity.Services;
using Infrastructure.Sqlite.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Sqlite.Identity.Extensions;

internal static class SqliteIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddSqliteIdentityStores(this IServiceCollection services)
    {
        services.AddScoped<IUserStore<User>, CustomUserStore>();
        services.AddScoped<IRoleStore<Role>, CustomRoleStore>();
        services.AddScoped<SqliteRoleRepository>();
        services.AddScoped<IRoleRepository>(sp => sp.GetRequiredService<SqliteRoleRepository>());
        services.AddScoped<IRoleLookup>(sp => sp.GetRequiredService<SqliteRoleRepository>());
        services.AddSingleton<IDatabaseBootstrap, IdentityTableInitializer>();
        return services;
    }
}
