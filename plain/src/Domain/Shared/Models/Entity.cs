using Domain.Shared.Interfaces;

namespace Domain.Shared.Models;

public abstract class Entity : IEntity
{
    public Guid Id { get; private set; }
    public Guid RevId { get; private set; } = Guid.NewGuid();

    protected void UpdateRevision() => RevId = Guid.NewGuid();

    // For ORM hydration
    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id cannot be empty", nameof(id));
        }

        Id = id;
    }
}
