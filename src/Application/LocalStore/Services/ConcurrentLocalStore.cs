using Application.Abstractions.LocalStore;
using DisposableHelpers.Attributes;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Infrastructure.Storage.Features;

[Disposable]
public partial class ConcurrentLocalStore(ILocalStoreService localStoreService) : IDisposable
{
    public required string Group { get; init; }

    public async Task<bool> Contains(string id, CancellationToken cancellationToken = default)
    {
        VerifyNotDisposed();
        return await localStoreService.Contains(Group, id, cancellationToken);
    }

    public async Task ContainsOrError(string id, CancellationToken cancellationToken = default)
    {
        if (!await Contains(id, cancellationToken))
        {
            throw new KeyNotFoundException($"The item with ID '{id}' does not exist in the group '{Group}'.");
        }
    }

    public async Task<string> Get(string id, CancellationToken cancellationToken = default)
    {
        VerifyNotDisposed();
        return await localStoreService.Get(Group, id, cancellationToken);
    }

    public async Task<T?> Get<T>(string id, JsonTypeInfo<T?> jsonTypeInfo, CancellationToken cancellationToken = default)
        where T : class
    {
        var value = await Get(id, cancellationToken);
        if (!string.IsNullOrEmpty(value))
        {
            return JsonSerializer.Deserialize(value, jsonTypeInfo);
        }
        else
        {
            return default;
        }
    }

    public async Task<string[]> GetIds(CancellationToken cancellationToken = default)
    {
        VerifyNotDisposed();
        return await localStoreService.GetIds(Group, cancellationToken);
    }

    public async Task Set(string id, string? value, CancellationToken cancellationToken = default)
    {
        VerifyNotDisposed();
        await localStoreService.Set(Group, id, value, cancellationToken);
    }

    public async Task Set<T>(string id, T? obj, JsonTypeInfo<T?> jsonTypeInfo, CancellationToken cancellationToken = default)
        where T : class
    {
        string data = JsonSerializer.Serialize(obj, jsonTypeInfo);
        await Set(id, data, cancellationToken);
    }

    public async Task Delete(string id, CancellationToken cancellationToken = default)
    {
        await Set(id, null, cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        VerifyNotDisposed();
        await localStoreService.CommitAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        VerifyNotDisposed();
        await localStoreService.RollbackAsync(cancellationToken);
    }

    protected void Dispose(bool disposing)
    {
        if (disposing)
        {
            localStoreService?.Dispose();
        }
    }
}