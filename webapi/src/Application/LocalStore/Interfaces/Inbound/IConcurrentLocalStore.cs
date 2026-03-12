namespace Application.LocalStore.Interfaces.Inbound;

/// <summary>
/// Provides concurrent access to a local key-value store with transaction support.
/// </summary>
public interface IConcurrentLocalStore : IDisposable
{
    string Group { get; }
    Task<bool> Contains(string id, CancellationToken cancellationToken = default);
    Task ContainsOrError(string id, CancellationToken cancellationToken = default);
    Task<string?> Get(string id, CancellationToken cancellationToken = default);
    Task<string[]> GetIds(CancellationToken cancellationToken = default);
    Task Set(string id, string? value, CancellationToken cancellationToken = default);
    Task Delete(string id, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
