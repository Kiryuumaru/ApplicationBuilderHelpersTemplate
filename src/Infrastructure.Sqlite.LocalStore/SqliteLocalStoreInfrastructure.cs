using Application.LocalStore.Interfaces;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Sqlite.LocalStore;

public class SqliteLocalStoreInfrastructure : SqliteInfrastructure
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddScoped<ILocalStoreService, SqliteLocalStoreService>();
        services.AddSingleton<IDatabaseBootstrap, LocalStoreTableInitializer>();
    }
}
