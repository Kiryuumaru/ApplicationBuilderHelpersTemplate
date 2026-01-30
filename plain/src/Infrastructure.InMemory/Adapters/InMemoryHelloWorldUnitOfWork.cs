using Application.HelloWorld.Interfaces.Outbound;
using Application.Shared.Interfaces;
using Domain.Shared.Interfaces;
using Infrastructure.InMemory.Interfaces;

namespace Infrastructure.InMemory.Adapters;

internal sealed class InMemoryHelloWorldUnitOfWork(
    IDomainEventDispatcher eventDispatcher,
    IEnumerable<ITrackableRepository> trackableRepositories) : IHelloWorldUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // Collect all tracked aggregates from repositories
        var trackedAggregates = trackableRepositories
            .SelectMany(r => r.GetTrackedAggregates())
            .ToList();

        // Collect all domain events from tracked aggregates
        var domainEvents = trackedAggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();

        // Clear events from entities (simulates what EF Core interceptor does)
        foreach (var aggregate in trackedAggregates)
        {
            aggregate.ClearDomainEvents();
        }

        // Clear tracking (data is already "persisted" in static storage)
        foreach (var repository in trackableRepositories)
        {
            repository.ClearTrackedAggregates();
        }

        // Dispatch events post-commit
        await eventDispatcher.DispatchAsync(domainEvents, cancellationToken);
    }
}
