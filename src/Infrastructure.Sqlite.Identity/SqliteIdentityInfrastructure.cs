using Application.Authorization.Roles.Interfaces;
using ApplicationBuilderHelpers;
using Domain.Authorization.Models;
using Domain.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Sqlite.Identity;

public class SqliteIdentityInfrastructure : SqliteInfrastructure
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddScoped<IUserStore<User>, CustomUserStore>();
        services.AddScoped<IRoleStore<Role>, CustomRoleStore>();
        services.AddScoped<SqliteRoleRepository>();
        services.AddScoped<IRoleRepository>(sp => sp.GetRequiredService<SqliteRoleRepository>());
        services.AddScoped<IRoleLookup>(sp => sp.GetRequiredService<SqliteRoleRepository>());
        services.AddSingleton<IDatabaseBootstrap, IdentityTableInitializer>();
    }
}
