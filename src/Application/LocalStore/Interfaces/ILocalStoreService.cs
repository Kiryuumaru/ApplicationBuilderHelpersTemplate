namespace Application.LocalStore.Interfaces;

public interface ILocalStoreService
{
    Task<string> Get(string group, string id, CancellationToken cancellationToken);

    Task<string[]> GetIds(string group, CancellationToken cancellationToken);

    Task Set(string group, string id, string? data, CancellationToken cancellationToken);

    Task<bool> Contains(string group, string id, CancellationToken cancellationToken);
}
