using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Sqlite;

public class SqliteInfrastructure : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        var connectionString = applicationBuilder.Configuration.GetRefValueOrDefault("SQLITE_CONNECTION_STRING", "Data Source=app.db");

        services.AddSingleton(new SqliteConnectionFactory(connectionString));
        services.AddHostedService<SqliteDatabaseBootstrapperWorker>();
    }
}
