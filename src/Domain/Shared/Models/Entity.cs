using Domain.Shared.Interfaces;

namespace Domain.Shared.Models;

public abstract class Entity<TId> : DomainObject, IEntity<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; private set; }

    protected Entity(TId id)
    {
        if (id is null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        Id = id;
    }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
