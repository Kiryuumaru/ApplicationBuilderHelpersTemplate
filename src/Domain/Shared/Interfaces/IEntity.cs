namespace Domain.Shared.Interfaces;

public interface IEntity<out TId>
{
    TId Id { get; }
}
