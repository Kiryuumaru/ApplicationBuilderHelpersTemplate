namespace Application.Shared.Interfaces.Outbound;

/// <summary>
/// Unit of work abstraction for coordinating persistence and domain event dispatch.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Commits all pending changes and dispatches domain events from tracked aggregates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
