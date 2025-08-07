using Application.LocalStore.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using TransactionHelpers;

namespace Application.LocalStore.Features;

public class ConcurrentLocalStore(ILocalStoreService localStore, IDisposable concurrencyTicket) : IDisposable
{
    private readonly ILocalStoreService _localStoreService = localStore;
    private readonly IDisposable _concurrencyTicket = concurrencyTicket;
    private bool _disposed = false;

    public required string Group { get; init; }

    public async Task<bool> Contains(string id, CancellationToken cancellationToken = default)
    {
        return await _localStoreService.Contains(Group, id, cancellationToken);
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
        return await _localStoreService.Get(Group, id, cancellationToken);
    }

    public async Task<T?> Get<T>(string id, JsonTypeInfo<T?> jsonTypeInfo, CancellationToken cancellationToken = default)
        where T : class
    {
        var value = await _localStoreService.Get(Group, id, cancellationToken);
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
        return await _localStoreService.GetIds(Group, cancellationToken);
    }

    public async Task Set(string id, string? value, CancellationToken cancellationToken = default)
    {
        await _localStoreService.Set(Group, id, value, cancellationToken);
    }

    public async Task Set<T>(string id, T? obj, JsonTypeInfo<T?> jsonTypeInfo, CancellationToken cancellationToken = default)
        where T : class
    {
        string data = JsonSerializer.Serialize(obj, jsonTypeInfo);
        await _localStoreService.Set(Group, id, data, cancellationToken);
    }

    public async Task Delete(string id, CancellationToken cancellationToken = default)
    {
        await _localStoreService.Set(Group, id, null, cancellationToken);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _concurrencyTicket?.Dispose();
            _disposed = true;
        }
    }
}
