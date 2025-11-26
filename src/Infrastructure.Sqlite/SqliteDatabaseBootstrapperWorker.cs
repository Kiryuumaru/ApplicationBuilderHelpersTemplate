using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Sqlite;

internal class SqliteDatabaseBootstrapperWorker(IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrappers = serviceProvider.GetServices<IDatabaseBootstrap>();
        foreach (var bootstrapper in bootstrappers)
        {
            await bootstrapper.SetupAsync(stoppingToken);
        }
    }
}
