using Application.Client.Authentication.Interfaces.Infrastructure;
using Application.Client.Authentication.Models;
using Application.LocalStore.Interfaces.Infrastructure;
using Blazored.LocalStorage;

namespace Presentation.WebApp.Client.Services;

internal class BlazoredLocalStorageStorage : ITokenStorage, ILocalStoreService
{
    private const string CredentialsKey = "auth_credentials";
    private const string LocalStorePrefix = "localstore_";
    private readonly ILocalStorageService _localStorage;

    private bool _isOpen;
    private bool _hasTransaction;

    // Tracks the final state of each key (null means delete)
    private readonly Dictionary<string, string?> _pendingState = [];

    // Tracks operation order - only the last operation per key matters for commit,
    // but we track which keys were touched to know what to apply
    private readonly List<string> _operationOrder = [];

    public BlazoredLocalStorageStorage(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    #region ITokenStorage Implementation

    public async Task StoreCredentialsAsync(StoredCredentials credentials)
    {
        await _localStorage.SetItemAsync(CredentialsKey, credentials);
    }

    public async Task<StoredCredentials?> GetCredentialsAsync()
    {
        try
        {
            return await _localStorage.GetItemAsync<StoredCredentials>(CredentialsKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task ClearCredentialsAsync()
    {
        await _localStorage.RemoveItemAsync(CredentialsKey);
    }

    public async Task<bool> HasCredentialsAsync()
    {
        return await _localStorage.ContainKeyAsync(CredentialsKey);
    }

    #endregion

    #region ILocalStoreService Implementation

    private static string BuildKey(string group, string id) => $"{LocalStorePrefix}{group}_{id}";

    private static string BuildGroupPrefix(string group) => $"{LocalStorePrefix}{group}_";

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            _isOpen = true;
            _hasTransaction = true;
            _pendingState.Clear();
            _operationOrder.Clear();
        }
    }

    public async Task<string?> Get(string group, string id, CancellationToken cancellationToken)
    {
        EnsureOpen();
        var key = BuildKey(group, id);

        // Check pending state first (includes both sets and deletes)
        if (_pendingState.TryGetValue(key, out var pendingValue))
        {
            return pendingValue; // Returns null if marked for deletion
        }

        // Fall back to actual storage
        try
        {
            return await _localStorage.GetItemAsStringAsync(key, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string[]> GetIds(string group, CancellationToken cancellationToken)
    {
        EnsureOpen();
        var prefix = BuildGroupPrefix(group);
        var keys = await _localStorage.KeysAsync(cancellationToken);
        var ids = new HashSet<string>();

        // Add IDs from actual storage
        foreach (var key in keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                // Check if pending state overrides this
                if (_pendingState.TryGetValue(key, out var pendingValue))
                {
                    // Only include if not marked for deletion
                    if (pendingValue is not null)
                    {
                        ids.Add(key[prefix.Length..]);
                    }
                }
                else
                {
                    ids.Add(key[prefix.Length..]);
                }
            }
        }

        // Add IDs from pending state that are new additions (not in storage yet)
        foreach (var (key, value) in _pendingState)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal) && value is not null)
            {
                ids.Add(key[prefix.Length..]);
            }
        }

        return [.. ids];
    }

    public Task Set(string group, string id, string? data, CancellationToken cancellationToken)
    {
        EnsureOpen();
        var key = BuildKey(group, id);

        // Track operation order for sequential commit
        if (!_pendingState.ContainsKey(key))
        {
            _operationOrder.Add(key);
        }

        // Store the latest state (null means delete)
        _pendingState[key] = data;

        return Task.CompletedTask;
    }

    public async Task<bool> Contains(string group, string id, CancellationToken cancellationToken)
    {
        EnsureOpen();
        var key = BuildKey(group, id);

        // Check pending state first
        if (_pendingState.TryGetValue(key, out var pendingValue))
        {
            return pendingValue is not null;
        }

        // Fall back to actual storage
        return await _localStorage.ContainKeyAsync(key, cancellationToken);
    }

    public Task Open(CancellationToken cancellationToken)
    {
        EnsureOpen();
        return Task.CompletedTask;
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (!_hasTransaction)
        {
            return;
        }

        // Apply operations in the order keys were first touched
        foreach (var key in _operationOrder)
        {
            if (_pendingState.TryGetValue(key, out var value))
            {
                if (value is null)
                {
                    await _localStorage.RemoveItemAsync(key, cancellationToken);
                }
                else
                {
                    await _localStorage.SetItemAsStringAsync(key, value, cancellationToken);
                }
            }
        }

        _pendingState.Clear();
        _operationOrder.Clear();
        _hasTransaction = false;
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (!_hasTransaction)
        {
            return Task.CompletedTask;
        }

        // Discard all pending changes without applying
        _pendingState.Clear();
        _operationOrder.Clear();
        _hasTransaction = false;

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Auto-commit on dispose (same behavior as EFCoreLocalStoreService)
        if (_hasTransaction)
        {
            CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        _pendingState.Clear();
        _operationOrder.Clear();
        _isOpen = false;

        GC.SuppressFinalize(this);
    }

    #endregion
}
