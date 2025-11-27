using Application.LocalStore.Common;
using Application.LocalStore.Interfaces;
using DisposableHelpers.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Storage.Features;

[Disposable]
public partial class ConcurrentLocalStore(ILocalStoreService localStoreService, string group) : IDisposable
{
    private readonly ILocalStoreService localStoreService = localStoreService ?? throw new ArgumentNullException(nameof(localStoreService));
    private readonly SemaphoreSlim gate = new(1, 1);

    public string Group { get; } = LocalStoreKey.NormalizeGroup(group);

    public Task<bool> Contains(string id, CancellationToken cancellationToken = default)
    {
        var normalizedId = LocalStoreKey.NormalizeId(id);
        return WithGateAsync(() => localStoreService.Contains(Group, normalizedId, cancellationToken), cancellationToken);
    }

    public async Task ContainsOrError(string id, CancellationToken cancellationToken = default)
    {
        if (!await Contains(id, cancellationToken).ConfigureAwait(false))
        {
            throw new KeyNotFoundException($"The item with ID '{id}' does not exist in the group '{Group}'.");
        }
    }

    public Task<string?> Get(string id, CancellationToken cancellationToken = default)
    {
        var normalizedId = LocalStoreKey.NormalizeId(id);
        return WithGateAsync(() => localStoreService.Get(Group, normalizedId, cancellationToken), cancellationToken);
    }

    public async Task<T?> Get<T>(string id, JsonTypeInfo<T?> jsonTypeInfo, CancellationToken cancellationToken = default)
        where T : class
    {
        var payload = await Get(id, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(payload))
        {
            return default;
        }

        return JsonSerializer.Deserialize(payload, jsonTypeInfo);
    }

    public Task<string[]> GetIds(CancellationToken cancellationToken = default)
    {
        return WithGateAsync(() => localStoreService.GetIds(Group, cancellationToken), cancellationToken);
    }

    public Task Set(string id, string? value, CancellationToken cancellationToken = default)
    {
        var normalizedId = LocalStoreKey.NormalizeId(id);
        return WithGateAsync(() => localStoreService.Set(Group, normalizedId, value, cancellationToken), cancellationToken);
    }

    public Task Set<T>(string id, T? obj, JsonTypeInfo<T?> jsonTypeInfo, CancellationToken cancellationToken = default)
        where T : class
    {
        var data = JsonSerializer.Serialize(obj, jsonTypeInfo);
        return Set(id, data, cancellationToken);
    }

    public Task SetBool(string id, bool value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v ? bool.TrueString : bool.FalseString, cancellationToken);

    public Task<bool?> GetBool(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => bool.Parse(s), cancellationToken);

    public Task SetByte(string id, byte value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString(CultureInfo.InvariantCulture), cancellationToken);

    public Task<byte?> GetByte(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => byte.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture), cancellationToken);

    public Task SetSByte(string id, sbyte value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString(CultureInfo.InvariantCulture), cancellationToken);

    public Task<sbyte?> GetSByte(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => sbyte.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture), cancellationToken);

    public Task SetShort(string id, short value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString(CultureInfo.InvariantCulture), cancellationToken);

    public Task<short?> GetShort(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => short.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture), cancellationToken);

    public Task SetUShort(string id, ushort value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString(CultureInfo.InvariantCulture), cancellationToken);

    public Task<ushort?> GetUShort(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => ushort.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture), cancellationToken);

    public Task SetInt(string id, int value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString(CultureInfo.InvariantCulture), cancellationToken);

    public Task<int?> GetInt(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture), cancellationToken);

    public Task SetUInt(string id, uint value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString(CultureInfo.InvariantCulture), cancellationToken);

    public Task<uint?> GetUInt(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => uint.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture), cancellationToken);

    public Task SetLong(string id, long value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString(CultureInfo.InvariantCulture), cancellationToken);

    public Task<long?> GetLong(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => long.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture), cancellationToken);

    public Task SetULong(string id, ulong value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString(CultureInfo.InvariantCulture), cancellationToken);

    public Task<ulong?> GetULong(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => ulong.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture), cancellationToken);

    public Task SetFloat(string id, float value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString("R", CultureInfo.InvariantCulture), cancellationToken);

    public Task<float?> GetFloat(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => float.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture), cancellationToken);

    public Task SetDouble(string id, double value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString("R", CultureInfo.InvariantCulture), cancellationToken);

    public Task<double?> GetDouble(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => double.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture), cancellationToken);

    public Task SetDecimal(string id, decimal value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString(CultureInfo.InvariantCulture), cancellationToken);

    public Task<decimal?> GetDecimal(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => decimal.Parse(s, NumberStyles.Number, CultureInfo.InvariantCulture), cancellationToken);

    public Task SetGuid(string id, Guid value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString("D", CultureInfo.InvariantCulture), cancellationToken);

    public Task<Guid?> GetGuid(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => Guid.Parse(s), cancellationToken);

    public Task SetChar(string id, char value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString(), cancellationToken);

    public Task<char?> GetChar(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => ParseChar(s), cancellationToken);

    public Task SetDateTime(string id, DateTime value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString("O", CultureInfo.InvariantCulture), cancellationToken);

    public Task<DateTime?> GetDateTime(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), cancellationToken);

    public Task SetDateTimeOffset(string id, DateTimeOffset value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString("O", CultureInfo.InvariantCulture), cancellationToken);

    public Task<DateTimeOffset?> GetDateTimeOffset(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), cancellationToken);

    public Task SetTimeSpan(string id, TimeSpan value, CancellationToken cancellationToken = default)
        => SetInvariant(id, value, static v => v.ToString("c", CultureInfo.InvariantCulture), cancellationToken);

    public Task<TimeSpan?> GetTimeSpan(string id, CancellationToken cancellationToken = default)
        => GetInvariant(id, static s => TimeSpan.Parse(s, CultureInfo.InvariantCulture), cancellationToken);

    public Task Delete(string id, CancellationToken cancellationToken = default)
    {
        return Set(id, null, cancellationToken);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        return WithGateAsync(() => localStoreService.CommitAsync(cancellationToken), cancellationToken);
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        return WithGateAsync(() => localStoreService.RollbackAsync(cancellationToken), cancellationToken);
    }

    private async Task<T> WithGateAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        VerifyNotDisposed();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task WithGateAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        await WithGateAsync(async () =>
        {
            await action().ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    protected void Dispose(bool disposing)
    {
        if (disposing)
        {
            gate.Dispose();
            localStoreService?.Dispose();
        }
    }

    private Task SetInvariant<T>(string id, T value, Func<T, string> formatter, CancellationToken cancellationToken)
        where T : struct
    {
        var formatted = formatter(value);
        return Set(id, formatted, cancellationToken);
    }

    private async Task<T?> GetInvariant<T>(string id, Func<string, T> parser, CancellationToken cancellationToken)
        where T : struct
    {
        var stored = await Get(id, cancellationToken).ConfigureAwait(false);
        return stored is null ? null : parser(stored);
    }

    private static char ParseChar(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 1)
        {
            throw new FormatException("Stored value is not a single character.");
        }

        return value[0];
    }
}