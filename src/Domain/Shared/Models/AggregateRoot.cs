using Domain.Shared.Interfaces;

namespace Domain.Shared.Models;

public abstract class AggregateRoot<TId> : AuditableEntity<TId>, IAggregateRoot
{
    protected AggregateRoot(TId id) : base(id)
    {
    }
}
