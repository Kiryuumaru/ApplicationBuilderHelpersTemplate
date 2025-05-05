﻿using Application.LocalStore.Interfaces;
using DisposableHelpers.Attributes;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using TransactionHelpers;

namespace Application.LocalStore.Features;

[Disposable]
public partial class ConcurrentLocalStore(ILocalStoreService localStore)
{
    private readonly ILocalStoreService _localStoreService = localStore;

    public required string Group { get; init; }

    public async Task<Result<bool>> Contains(string id, CancellationToken cancellationToken = default)
    {
        Result<bool> result = new();

        if (!result.Success(await _localStoreService.Get(Group, id, cancellationToken), out string? value))
        {
            return result;
        }

        result.WithValue(!string.IsNullOrEmpty(value));

        return result;
    }

    public async Task<Result> ContainsOrError(string id, CancellationToken cancellationToken = default)
    {
        Result result = new();

        if (!result.Success(await _localStoreService.Get(Group, id, cancellationToken), out string? value))
        {
            return result;
        }

        if (string.IsNullOrEmpty(value))
        {
            result.WithError(new Exception(id + " does not exists"));
        }

        return result;
    }

    public async Task<Result<string>> Get(string id, CancellationToken cancellationToken = default)
    {
        Result<string> result = new();

        if (!result.Success(await _localStoreService.Get(Group, id, cancellationToken), out string? value))
        {
            return result;
        }

        try
        {
            if (!string.IsNullOrEmpty(value))
            {
                result.WithValue(value);
            }
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

        if (!result.Success(await _localStoreService.Get(Group, id, cancellationToken), out string? value))
        {
            return result;
        }

        try
        {
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

        if (!result.Success(await _localStoreService.GetIds(Group, cancellationToken), out string[]? ids))
        {
            return result;
        }

        result.WithValue(ids);

        return result;
    }

    public async Task<Result> Set(string id, string? value, CancellationToken cancellationToken = default)
    {
        Result result = new();

        if (!result.Success(await _localStoreService.Set(Group, id, value, cancellationToken)))
        {
            return result;
        }

        return result;
    }

    public async Task<Result> Set<T>(string id, T? obj, JsonTypeInfo<T?> jsonTypeInfo, CancellationToken cancellationToken = default)
        where T : class
    {
        Result result = new();

        string data = JsonSerializer.Serialize(obj, jsonTypeInfo);

        if (!result.Success(await _localStoreService.Set(Group, id, data, cancellationToken)))
        {
            return result;
        }

        return result;
    }

    public async Task<Result<bool>> Delete(string id, CancellationToken cancellationToken = default)
    {
        Result<bool> result = new();

        if (!result.Success(await _localStoreService.Get(Group, id, cancellationToken), out string? value))
        {
            return result;
        }

        if (!result.Success(await _localStoreService.Set(Group, id, null, cancellationToken)))
        {
            return result;
        }

        result.WithValue(!string.IsNullOrEmpty(value));

        return result;
    }
}
