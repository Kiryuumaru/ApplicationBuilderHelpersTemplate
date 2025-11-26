using Domain.Shared.Interfaces;

namespace Domain.Shared.Models;

public abstract class AggregateRoot : AuditableEntity, IAggregateRoot
{
    protected AggregateRoot(Guid id) : base(id)
    {
    }
}
