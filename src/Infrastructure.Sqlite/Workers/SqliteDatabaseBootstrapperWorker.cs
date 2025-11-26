using Infrastructure.Sqlite.Interfaces;
using Infrastructure.Sqlite.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Sqlite.Workers;

internal class SqliteDatabaseBootstrapperWorker(IServiceProvider serviceProvider, DatabaseInitializationState initializationState) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        var bootstrappers = serviceProvider.GetServices<IDatabaseBootstrap>();
        foreach (var bootstrapper in bootstrappers)
        {
            await bootstrapper.SetupAsync(cancellationToken);
        }
        initializationState.MarkInitialized();
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
