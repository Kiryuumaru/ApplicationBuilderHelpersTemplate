using Infrastructure.EFCore.Interfaces;
using Infrastructure.EFCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.EFCore.Workers;

internal sealed class EFCoreDatabaseBootstrapperWorker(
    IServiceProvider serviceProvider,
    EFCoreDatabaseInitializationState initializationState) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var bootstrappers = scope.ServiceProvider.GetServices<IEFCoreDatabaseBootstrap>();
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
