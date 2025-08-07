using Application.LocalStore.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using TransactionHelpers;

namespace Application.LocalStore.Features;

public class ConcurrentLocalStore : IDisposable
{
    private readonly ILocalStoreService _localStoreService;
    private readonly IDisposable _concurrencyTicket;
    private bool _disposed = false;

    public required string Group { get; init; }

    public ConcurrentLocalStore(ILocalStoreService localStore, IDisposable concurrencyTicket)
    {
        _localStoreService = localStore;
        _concurrencyTicket = concurrencyTicket;
    }

    public async Task<Result<bool>> Contains(string id, CancellationToken cancellationToken = default)
    {
        Result<bool> result = new();

        try
        {
            var contains = await _localStoreService.Contains(Group, id, cancellationToken);
            result.WithValue(contains);
        }
        catch (Exception ex)
        {
            result.WithError(ex);
        }

        return result;
    }

    public async Task<Result> ContainsOrError(string id, CancellationToken cancellationToken = default)
    {
        Result result = new();

        try
        {
            var value = await _localStoreService.Get(Group, id, cancellationToken);
            if (string.IsNullOrEmpty(value))
            {
                result.WithError(new Exception(id + " does not exists"));
            }
        }
        catch (Exception ex)
        {
            result.WithError(ex);
        }

        return result;
    }

    public async Task<Result<string>> Get(string id, CancellationToken cancellationToken = default)
    {
        Result<string> result = new();

        try
        {
            var value = await _localStoreService.Get(Group, id, cancellationToken);
            result.WithValue(value);
        }
        catch (Exception ex)
        {
            result.WithError(ex);
        }

        return result;
    }

    public async Task<Result<T>> Get<T>(string id, JsonTypeInfo<T?> jsonTypeInfo, CancellationToken cancellationToken = default)
        where T : class
    {
        Result<T> result = new();

        try
        {
            var value = await _localStoreService.Get(Group, id, cancellationToken);
            if (!string.IsNullOrEmpty(value))
            {
                T? obj = JsonSerializer.Deserialize(value, jsonTypeInfo);
                result.WithValue(obj);
            }
            else
            {
                result.WithValue((T?)default);
            }
        }
        catch (Exception ex)
        {
            result.WithError(ex);
        }

        return result;
    }

    public async Task<Result<string[]>> GetIds(CancellationToken cancellationToken = default)
    {
        Result<string[]> result = new();

        try
        {
            var ids = await _localStoreService.GetIds(Group, cancellationToken);
            result.WithValue(ids);
        }
        catch (Exception ex)
        {
            result.WithError(ex);
        }

        return result;
    }

    public async Task<Result> Set(string id, string? value, CancellationToken cancellationToken = default)
    {
        Result result = new();

        try
        {
            await _localStoreService.Set(Group, id, value, cancellationToken);
        }
        catch (Exception ex)
        {
            result.WithError(ex);
        }

        return result;
    }

    public async Task<Result> Set<T>(string id, T? obj, JsonTypeInfo<T?> jsonTypeInfo, CancellationToken cancellationToken = default)
        where T : class
    {
        Result result = new();

        try
        {
            string data = JsonSerializer.Serialize(obj, jsonTypeInfo);
            await _localStoreService.Set(Group, id, data, cancellationToken);
        }
        catch (Exception ex)
        {
            result.WithError(ex);
        }

        return result;
    }

    public async Task<Result<bool>> Delete(string id, CancellationToken cancellationToken = default)
    {
        Result<bool> result = new();

        try
        {
            var value = await _localStoreService.Get(Group, id, cancellationToken);
            bool hadValue = !string.IsNullOrEmpty(value);
            
            await _localStoreService.Set(Group, id, null, cancellationToken);
            result.WithValue(hadValue);
        }
        catch (Exception ex)
        {
            result.WithError(ex);
        }

        return result;
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
