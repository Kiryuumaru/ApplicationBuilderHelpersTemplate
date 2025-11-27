using System.Collections.Concurrent;
using Domain.Identity.Models;
using Microsoft.AspNetCore.Identity;

namespace Application.Identity.Services;

internal sealed class InMemoryUserStore : IUserStore<User>, IUserPasswordStore<User>, IUserLoginStore<User>
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public Task AddLoginAsync(User user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(login);

        user.LinkIdentity(login.LoginProvider, login.ProviderKey, null, login.ProviderDisplayName);
        return Task.FromResult(0);
    }

    public Task RemoveLoginAsync(User user, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        
        user.UnlinkIdentity(loginProvider, providerKey);
        return Task.FromResult(0);
    }

    public Task<IList<UserLoginInfo>> GetLoginsAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        var logins = user.IdentityLinks
            .Select(l => new UserLoginInfo(l.Provider, l.Subject, l.DisplayName))
            .ToList();

        return Task.FromResult<IList<UserLoginInfo>>(logins);
    }

    public Task<User?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = _users.Values.FirstOrDefault(u => u.HasIdentity(loginProvider, providerKey));
        return Task.FromResult(user);
    }

    public Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        if (!_users.TryAdd(user.Id, user))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError { Description = "User already exists." }));
        }

        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        _users.TryRemove(user.Id, out _);
        return Task.FromResult(IdentityResult.Success);
    }

    public void Dispose()
    {
        // No resources to dispose
    }

    public Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (Guid.TryParse(userId, out var guid))
        {
            if (_users.TryGetValue(guid, out var user))
            {
                return Task.FromResult<User?>(user);
            }
        }

        return Task.FromResult<User?>(null);
    }

    public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var user = _users.Values.FirstOrDefault(u => u.NormalizedUserName == normalizedUserName);
        return Task.FromResult(user);
    }

    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult<string?>(user.NormalizedUserName);
    }

    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.Id.ToString());
    }

    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult<string?>(user.UserName);
    }

    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        
        if (normalizedName != null)
        {
            user.SetNormalizedUserName(normalizedName);
        }

        return Task.CompletedTask;
    }

    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        
        if (userName != null)
        {
            user.SetUserName(userName);
        }

        return Task.CompletedTask;
    }

    public Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        _users[user.Id] = user;
        return Task.FromResult(IdentityResult.Success);
    }

    // IUserPasswordStore implementation

    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.PasswordHash);
    }

    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.PasswordHash != null);
    }

    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        
        user.SetPasswordHash(passwordHash);

        return Task.CompletedTask;
    }
}
