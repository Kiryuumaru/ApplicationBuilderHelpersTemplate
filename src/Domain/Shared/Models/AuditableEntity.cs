namespace Domain.Shared.Models;

public abstract class AuditableEntity<TId> : Entity<TId>
{
    protected AuditableEntity(TId id) : base(id)
    {
    }

    public DateTimeOffset Created { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastModified { get; private set; } = DateTimeOffset.UtcNow;

    protected void MarkAsModified()
    {
        LastModified = DateTimeOffset.UtcNow;
    }
}
