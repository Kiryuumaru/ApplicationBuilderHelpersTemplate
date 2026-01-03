using Domain.Shared.Interfaces;

namespace Domain.Shared.Models;

public abstract class AggregateRoot(Guid id) : AuditableEntity(id), IAggregateRoot
{
}
