using System.Collections.Concurrent;
using Domain.HelloWorld.Entities;
using Domain.Shared.Interfaces;
using Infrastructure.InMemory.Interfaces;

namespace Infrastructure.InMemory.Repositories;

internal sealed class InMemoryHelloWorldRepository : Application.HelloWorld.Interfaces.Outbound.IHelloWorldRepository, ITrackableRepository
{
    // Shared storage across scopes (simulates database persistence)
    private static readonly ConcurrentDictionary<Guid, HelloWorldEntity> _storage = new();

    // Tracks entities added in this scope (for UoW to collect events)
    private readonly List<IAggregateRoot> _trackedAggregates = [];

    public void Add(HelloWorldEntity entity)
    {
        _storage[entity.Id] = entity;
        _trackedAggregates.Add(entity);
    }

    public Task<HelloWorldEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _storage.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<HelloWorldEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<HelloWorldEntity>>(_storage.Values.ToList());
    }

    public IEnumerable<IAggregateRoot> GetTrackedAggregates() => _trackedAggregates;

    public void ClearTrackedAggregates() => _trackedAggregates.Clear();
}
