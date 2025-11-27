using Domain.Authorization.Models;
using Domain.Identity.Models;
using Domain.Identity.ValueObjects;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EFCore.Identity.Services;

public class EFCoreUserStore(EFCoreDbContext dbContext) :
    IUserStore<User>,
    IUserPasswordStore<User>,
    IUserEmailStore<User>,
    IUserRoleStore<User>,
    IUserSecurityStampStore<User>,
    IUserLockoutStore<User>,
    IUserPhoneNumberStore<User>,
    IUserTwoFactorStore<User>,
    IUserAuthenticatorKeyStore<User>,
    IUserTwoFactorRecoveryCodeStore<User>,
    IUserLoginStore<User>
{
    private readonly EFCoreDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    
    private DbSet<User> Users => _dbContext.Set<User>();
    private DbSet<Role> Roles => _dbContext.Set<Role>();

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (DbUpdateException)
        {
            return IdentityResult.Failed(new IdentityError { Description = "User already exists." });
        }
    }

    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            Users.Update(user);
            var rows = await _dbContext.SaveChangesAsync(cancellationToken);
            if (rows == 0)
            {
                return IdentityResult.Failed(new IdentityError { Description = "User not found." });
            }
            return IdentityResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return IdentityResult.Failed(new IdentityError { Description = "Concurrency conflict." });
        }
    }

    public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var guid)) return null;
        return await Users.FindAsync([guid], cancellationToken);
    }

    public async Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return await Users
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);
    }

    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult<string?>(user.UserName);

    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken)
    {
        if (userName != null) user.SetUserName(userName);
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult<string?>(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken)
    {
        if (normalizedName != null) user.SetNormalizedUserName(normalizedName);
        return Task.CompletedTask;
    }

    // IUserPasswordStore
    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.SetPasswordHash(passwordHash);
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.PasswordHash != null);

    // IUserEmailStore
    public Task SetEmailAsync(User user, string? email, CancellationToken cancellationToken)
    {
        user.SetEmail(email);
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        user.SetEmailConfirmed(confirmed);
        return Task.CompletedTask;
    }

    public async Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return await Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.SetNormalizedEmail(normalizedEmail);
        return Task.CompletedTask;
    }

    // IUserRoleStore - These require related tables which we handle separately
    public async Task AddToRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        var role = await Roles
            .FirstOrDefaultAsync(r => r.NormalizedName == roleName.ToUpperInvariant(), cancellationToken);
        
        if (role == null)
            throw new InvalidOperationException($"Role {roleName} not found.");

        // Use reflection to access the private _roleAssignments field for in-memory update
        var rolesField = typeof(User).GetField("_roleAssignments", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var rolesSet = (HashSet<UserRoleAssignment>)rolesField!.GetValue(user)!;
        rolesSet.Add(UserRoleAssignment.Create(role.Id));
    }

    public async Task RemoveFromRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        var role = await Roles
            .FirstOrDefaultAsync(r => r.NormalizedName == roleName.ToUpperInvariant(), cancellationToken);
        
        if (role == null) return;

        var rolesField = typeof(User).GetField("_roleAssignments", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var rolesSet = (HashSet<UserRoleAssignment>)rolesField!.GetValue(user)!;
        var assignmentToRemove = rolesSet.FirstOrDefault(r => r.RoleId == role.Id);
        if (assignmentToRemove != null)
        {
            rolesSet.Remove(assignmentToRemove);
        }
    }

    public Task<IList<string>> GetRolesAsync(User user, CancellationToken cancellationToken)
    {
        // For now, return from the in-memory collection
        // In a full implementation, this would query the UserRoles join table
        IList<string> roles = user.RoleAssignments
            .Select(ra => ra.RoleId.ToString())
            .ToList();
        return Task.FromResult(roles);
    }

    public async Task<bool> IsInRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        var role = await Roles
            .FirstOrDefaultAsync(r => r.NormalizedName == roleName.ToUpperInvariant(), cancellationToken);
        
        if (role == null) return false;
        
        return user.RoleIds.Contains(role.Id);
    }

    public Task<IList<User>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        // This requires UserRoles join table which is not modeled in the simplified DbContext
        // For now, return empty list
        IList<User> users = [];
        return Task.FromResult(users);
    }

    // IUserSecurityStampStore
    public Task SetSecurityStampAsync(User user, string stamp, CancellationToken cancellationToken)
    {
        user.SetSecurityStamp(stamp);
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.SecurityStamp);

    // IUserLockoutStore
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(User user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.SetLockoutEnd(lockoutEnd);
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        var count = user.AccessFailedCount + 1;
        user.SetAccessFailedCount(count);
        return Task.FromResult(count);
    }

    public Task ResetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        user.SetAccessFailedCount(0);
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        user.SetLockoutEnabled(enabled);
        return Task.CompletedTask;
    }

    // IUserPhoneNumberStore
    public Task SetPhoneNumberAsync(User user, string? phoneNumber, CancellationToken cancellationToken)
    {
        user.SetPhoneNumber(phoneNumber);
        return Task.CompletedTask;
    }

    public Task<string?> GetPhoneNumberAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.PhoneNumber);

    public Task<bool> GetPhoneNumberConfirmedAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.PhoneNumberConfirmed);

    public Task SetPhoneNumberConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        user.SetPhoneNumberConfirmed(confirmed);
        return Task.CompletedTask;
    }

    // IUserTwoFactorStore
    public Task SetTwoFactorEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        user.SetTwoFactorEnabled(enabled);
        return Task.CompletedTask;
    }

    public Task<bool> GetTwoFactorEnabledAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.TwoFactorEnabled);

    // IUserAuthenticatorKeyStore
    public Task SetAuthenticatorKeyAsync(User user, string key, CancellationToken cancellationToken)
    {
        user.SetAuthenticatorKey(key);
        return Task.CompletedTask;
    }

    public Task<string?> GetAuthenticatorKeyAsync(User user, CancellationToken cancellationToken) 
        => Task.FromResult(user.AuthenticatorKey);

    // IUserTwoFactorRecoveryCodeStore
    public Task ReplaceCodesAsync(User user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
    {
        user.SetRecoveryCodes(string.Join(";", recoveryCodes));
        return Task.CompletedTask;
    }

    public Task<bool> RedeemCodeAsync(User user, string code, CancellationToken cancellationToken)
    {
        var codes = user.RecoveryCodes?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [];
        if (codes.Remove(code))
        {
            user.SetRecoveryCodes(string.Join(";", codes));
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<int> CountCodesAsync(User user, CancellationToken cancellationToken)
    {
        var count = user.RecoveryCodes?.Split(';', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        return Task.FromResult(count);
    }

    // IUserLoginStore - Simplified implementation without UserLogins table
    public Task AddLoginAsync(User user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        // Would require UserLogins join table
        return Task.CompletedTask;
    }

    public Task RemoveLoginAsync(User user, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        // Would require UserLogins join table
        return Task.CompletedTask;
    }

    public Task<IList<UserLoginInfo>> GetLoginsAsync(User user, CancellationToken cancellationToken)
    {
        IList<UserLoginInfo> logins = [];
        return Task.FromResult(logins);
    }

    public Task<User?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        return Task.FromResult<User?>(null);
    }

    public void Dispose()
    {
        // DbContext is managed by DI
    }
}
