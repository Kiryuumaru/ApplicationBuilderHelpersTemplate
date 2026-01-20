using Application.LocalStore.Interfaces.Infrastructure;
using Microsoft.JSInterop;

namespace Infrastructure.Browser.IndexedDB.LocalStore.Services;

/// <summary>
/// IndexedDB-based implementation of ILocalStoreService for browser persistence.
/// Uses JavaScript interop to interact with the browser's IndexedDB API.
/// </summary>
public sealed class IndexedDBLocalStoreService : ILocalStoreService
{
    private const string DatabaseName = "LocalStoreDB";
    private const string StoreName = "LocalStore";
    private const int DatabaseVersion = 1;
    
    private readonly IJSRuntime _jsRuntime;
    private readonly List<PendingOperation> _pendingOperations = [];
    private bool _isOpen;
    private bool _disposed;

    public IndexedDBLocalStoreService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    public Task Open(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        _isOpen = true;
        return Task.CompletedTask;
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        
        if (_pendingOperations.Count == 0)
        {
            return;
        }

        var operations = _pendingOperations.ToArray();
        _pendingOperations.Clear();

        await _jsRuntime.InvokeVoidAsync(
            "indexedDBLocalStore.commitOperations",
            cancellationToken,
            DatabaseName,
            StoreName,
            DatabaseVersion,
            operations.Select(o => new { o.Type, o.Group, o.Id, o.Data }).ToArray());
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        _pendingOperations.Clear();
        return Task.CompletedTask;
    }

    public async Task<string?> Get(string group, string id, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await EnsureOpenAsync(cancellationToken);

        // Check pending operations first (newest to oldest)
        for (int i = _pendingOperations.Count - 1; i >= 0; i--)
        {
            var op = _pendingOperations[i];
            if (op.Group == group && op.Id == id)
            {
                return op.Type == OperationType.Delete ? null : op.Data;
            }
        }

        return await _jsRuntime.InvokeAsync<string?>(
            "indexedDBLocalStore.get",
            cancellationToken,
            DatabaseName,
            StoreName,
            DatabaseVersion,
            group,
            id);
    }

    public async Task<string[]> GetIds(string group, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await EnsureOpenAsync(cancellationToken);

        var idsFromDb = await _jsRuntime.InvokeAsync<string[]>(
            "indexedDBLocalStore.getIds",
            cancellationToken,
            DatabaseName,
            StoreName,
            DatabaseVersion,
            group);

        // Apply pending operations to the result
        var resultSet = new HashSet<string>(idsFromDb);
        
        foreach (var op in _pendingOperations.Where(o => o.Group == group))
        {
            if (op.Type == OperationType.Delete)
            {
                resultSet.Remove(op.Id);
            }
            else
            {
                resultSet.Add(op.Id);
            }
        }

        return [.. resultSet];
    }

    public Task Set(string group, string id, string? data, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        
        if (data == null)
        {
            _pendingOperations.Add(new PendingOperation(OperationType.Delete, group, id, null));
        }
        else
        {
            _pendingOperations.Add(new PendingOperation(OperationType.Set, group, id, data));
        }

        return Task.CompletedTask;
    }

    public async Task<bool> Contains(string group, string id, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await EnsureOpenAsync(cancellationToken);

        // Check pending operations first (newest to oldest)
        for (int i = _pendingOperations.Count - 1; i >= 0; i--)
        {
            var op = _pendingOperations[i];
            if (op.Group == group && op.Id == id)
            {
                return op.Type != OperationType.Delete;
            }
        }

        return await _jsRuntime.InvokeAsync<bool>(
            "indexedDBLocalStore.contains",
            cancellationToken,
            DatabaseName,
            StoreName,
            DatabaseVersion,
            group,
            id);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Auto-commit pending operations on dispose (matches EFCore behavior)
        if (_pendingOperations.Count > 0 && _isOpen)
        {
            try
            {
                // Fire and forget - we can't await in Dispose
                _ = CommitAsync(CancellationToken.None);
            }
            catch
            {
                // Ignore errors during dispose
            }
        }

        _pendingOperations.Clear();
        _disposed = true;
    }

    private Task EnsureOpenAsync(CancellationToken cancellationToken)
    {
        if (!_isOpen)
        {
            return Open(cancellationToken);
        }
        return Task.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private enum OperationType
    {
        Set,
        Delete
    }

    private sealed record PendingOperation(OperationType Type, string Group, string Id, string? Data);
}
