using System.Collections.Concurrent;
using System.Linq;
using Application.Identity.Interfaces;
using Domain.Identity.Models;

namespace Application.Identity.Services;

internal sealed class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();
    private readonly ConcurrentDictionary<string, Guid> _usernameIndex = new(StringComparer.OrdinalIgnoreCase);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_users.TryGetValue(id, out var user) ? user : null);
    }

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(username))
        {
            return Task.FromResult<User?>(null);
        }

        var normalized = username.Trim();
        if (_usernameIndex.TryGetValue(normalized, out var userId) && _users.TryGetValue(userId, out var user))
        {
            return Task.FromResult<User?>(user);
        }

        return Task.FromResult<User?>(null);
    }

    public Task<User?> FindByExternalIdentityAsync(string provider, string providerSubject, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerSubject))
        {
            return Task.FromResult<User?>(null);
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var normalizedSubject = providerSubject.Trim();

        foreach (var user in _users.Values)
        {
            if (user.IdentityLinks.Any(link =>
                    string.Equals(link.Provider, normalizedProvider, StringComparison.Ordinal) &&
                    string.Equals(link.Subject, normalizedSubject, StringComparison.Ordinal)))
            {
                return Task.FromResult<User?>(user);
            }
        }

        return Task.FromResult<User?>(null);
    }

    public Task SaveAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        _users[user.Id] = user;
        _usernameIndex[user.Username] = user.Id;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<User>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = _users.Values
            .OrderBy(static u => u.Username, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyCollection<User>>(snapshot);
    }
}
