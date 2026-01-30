using Domain.HelloWorld.Entities;

namespace Application.HelloWorld.Interfaces.Outbound;

/// <summary>
/// Repository for HelloWorld entities.
/// </summary>
public interface IHelloWorldRepository
{
    /// <summary>
    /// Adds an entity to be tracked for persistence.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    void Add(HelloWorldEntity entity);

    /// <summary>
    /// Gets an entity by its identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    Task<HelloWorldEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entities.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All entities.</returns>
    Task<IReadOnlyList<HelloWorldEntity>> GetAllAsync(CancellationToken cancellationToken = default);
}
