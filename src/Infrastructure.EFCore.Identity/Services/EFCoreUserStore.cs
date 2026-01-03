using Application.Identity.Interfaces;
using Domain.Authorization.Models;
using Domain.Identity.Models;
using Domain.Identity.ValueObjects;
using Infrastructure.EFCore.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
    IUserLoginStore<User>,
    IUserPasskeyStore<User>,
    Application.Identity.Interfaces.IUserStore
{
    private readonly EFCoreDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    
    private DbSet<User> Users => _dbContext.Set<User>();
    private DbSet<Role> Roles => _dbContext.Set<Role>();
    private DbSet<UserLoginEntity> UserLogins => _dbContext.Set<UserLoginEntity>();
    private DbSet<UserPasskeyEntity> UserPasskeys => _dbContext.Set<UserPasskeyEntity>();
    private DbSet<UserRoleAssignmentEntity> UserRoleAssignments => _dbContext.Set<UserRoleAssignmentEntity>();

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
            // Save role assignments after user is created
            await SaveRoleAssignmentsAsync(user, cancellationToken);
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
            // Save role assignments when updating user
            await SaveRoleAssignmentsAsync(user, cancellationToken);
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

        // Remove role assignments first
        var roleAssignments = await UserRoleAssignments
            .Where(ura => ura.UserId == user.Id)
            .ToListAsync(cancellationToken);
        UserRoleAssignments.RemoveRange(roleAssignments);

        Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var guid)) return null;
        var user = await Users.FindAsync([guid], cancellationToken);
        if (user != null)
        {
            await LoadIdentityLinksAsync(user, cancellationToken);
        }
        return user;
    }

    public async Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        var user = await Users
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);
        if (user != null)
        {
            await LoadIdentityLinksAsync(user, cancellationToken);
        }
        return user;
    }

    private async Task LoadIdentityLinksAsync(User user, CancellationToken cancellationToken)
    {
        var logins = await UserLogins
            .Where(ul => ul.UserId == user.Id)
            .ToListAsync(cancellationToken);

        foreach (var login in logins)
        {
            user.LinkIdentity(login.LoginProvider, login.ProviderKey, login.Email, login.ProviderDisplayName);
        }

        // Load role assignments
        await LoadRoleAssignmentsAsync(user, cancellationToken);
    }

    private async Task LoadRoleAssignmentsAsync(User user, CancellationToken cancellationToken)
    {
        var roleAssignments = await UserRoleAssignments
            .Where(ura => ura.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var rolesField = typeof(User).GetField("_roleAssignments", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var rolesSet = (HashSet<UserRoleAssignment>)rolesField!.GetValue(user)!;
        rolesSet.Clear();

        foreach (var assignment in roleAssignments)
        {
            IReadOnlyDictionary<string, string?>? parameters = null;
            if (!string.IsNullOrEmpty(assignment.ParameterValuesJson))
            {
                parameters = JsonSerializer.Deserialize<Dictionary<string, string?>>(assignment.ParameterValuesJson);
            }
            rolesSet.Add(UserRoleAssignment.Create(assignment.RoleId, parameters));
        }
    }

    private async Task SaveRoleAssignmentsAsync(User user, CancellationToken cancellationToken)
    {
        // Get existing assignments from DB
        var existingAssignments = await UserRoleAssignments
            .Where(ura => ura.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var currentRoleIds = user.RoleAssignments.Select(ra => ra.RoleId).ToHashSet();
        var existingRoleIds = existingAssignments.Select(ea => ea.RoleId).ToHashSet();

        // Remove assignments that are no longer present
        var toRemove = existingAssignments.Where(ea => !currentRoleIds.Contains(ea.RoleId)).ToList();
        UserRoleAssignments.RemoveRange(toRemove);

        // Add new assignments
        foreach (var assignment in user.RoleAssignments)
        {
            if (!existingRoleIds.Contains(assignment.RoleId))
            {
                var entity = new UserRoleAssignmentEntity
                {
                    UserId = user.Id,
                    RoleId = assignment.RoleId,
                    ParameterValuesJson = assignment.ParameterValues.Count > 0
                        ? JsonSerializer.Serialize(assignment.ParameterValues)
                        : null,
                    AssignedAt = DateTimeOffset.UtcNow
                };
                UserRoleAssignments.Add(entity);
            }
            else
            {
                // Update existing assignment's parameters
                var existing = existingAssignments.First(ea => ea.RoleId == assignment.RoleId);
                existing.ParameterValuesJson = assignment.ParameterValues.Count > 0
                    ? JsonSerializer.Serialize(assignment.ParameterValues)
                    : null;
            }
        }
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

    // IUserLoginStore - Full implementation with UserLogins table
    public async Task AddLoginAsync(User user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(login);

        // Also link in the domain model
        user.LinkIdentity(login.LoginProvider, login.ProviderKey, null, login.ProviderDisplayName);

        var userLogin = new UserLoginEntity
        {
            LoginProvider = login.LoginProvider,
            ProviderKey = login.ProviderKey,
            UserId = user.Id,
            ProviderDisplayName = login.ProviderDisplayName,
            LinkedAt = DateTimeOffset.UtcNow
        };
        UserLogins.Add(userLogin);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveLoginAsync(User user, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        // Also unlink in the domain model
        user.UnlinkIdentity(loginProvider, providerKey);

        var login = await UserLogins.FirstOrDefaultAsync(
            ul => ul.UserId == user.Id && ul.LoginProvider == loginProvider && ul.ProviderKey == providerKey,
            cancellationToken);

        if (login != null)
        {
            UserLogins.Remove(login);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        var logins = await UserLogins
            .Where(ul => ul.UserId == user.Id)
            .Select(ul => new UserLoginInfo(ul.LoginProvider, ul.ProviderKey, ul.ProviderDisplayName))
            .ToListAsync(cancellationToken);

        return logins;
    }

    public async Task<User?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var userLogin = await UserLogins
            .FirstOrDefaultAsync(ul => ul.LoginProvider == loginProvider && ul.ProviderKey == providerKey, cancellationToken);

        if (userLogin == null) return null;

        return await Users.FindAsync([userLogin.UserId], cancellationToken);
    }

    // IUserPasskeyStore implementation
    public async Task<IList<UserPasskeyInfo>> GetPasskeysAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        var passkeys = await UserPasskeys
            .Where(up => up.UserId == user.Id)
            .ToListAsync(cancellationToken);

        return passkeys.Select(p => new UserPasskeyInfo(
            p.CredentialId,
            p.PublicKey ?? [],
            p.CreatedAt,
            p.SignCount,
            string.IsNullOrEmpty(p.Transports) ? null : System.Text.Json.JsonSerializer.Deserialize<string[]>(p.Transports),
            p.IsUserVerified,
            p.IsBackupEligible,
            p.IsBackedUp,
            p.AttestationObject ?? [],
            p.ClientDataJson ?? []
        )).ToList();
    }

    public async Task<UserPasskeyInfo?> FindPasskeyAsync(User user, byte[] credentialId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(credentialId);

        var passkey = await UserPasskeys
            .FirstOrDefaultAsync(up => up.UserId == user.Id && up.CredentialId == credentialId, cancellationToken);

        if (passkey == null) return null;

        return new UserPasskeyInfo(
            passkey.CredentialId,
            passkey.PublicKey ?? [],
            passkey.CreatedAt,
            passkey.SignCount,
            string.IsNullOrEmpty(passkey.Transports) ? null : System.Text.Json.JsonSerializer.Deserialize<string[]>(passkey.Transports),
            passkey.IsUserVerified,
            passkey.IsBackupEligible,
            passkey.IsBackedUp,
            passkey.AttestationObject ?? [],
            passkey.ClientDataJson ?? []
        );
    }

    public async Task AddOrUpdatePasskeyAsync(User user, UserPasskeyInfo passkeyInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(passkeyInfo);

        var existing = await UserPasskeys
            .FirstOrDefaultAsync(up => up.UserId == user.Id && up.CredentialId == passkeyInfo.CredentialId, cancellationToken);

        if (existing != null)
        {
            existing.PublicKey = passkeyInfo.PublicKey;
            existing.SignCount = passkeyInfo.SignCount;
            existing.Transports = passkeyInfo.Transports?.Length > 0
                ? System.Text.Json.JsonSerializer.Serialize(passkeyInfo.Transports)
                : null;
            existing.IsUserVerified = passkeyInfo.IsUserVerified;
            existing.IsBackupEligible = passkeyInfo.IsBackupEligible;
            existing.IsBackedUp = passkeyInfo.IsBackedUp;
            existing.AttestationObject = passkeyInfo.AttestationObject;
            existing.ClientDataJson = passkeyInfo.ClientDataJson;
        }
        else
        {
            var entity = new UserPasskeyEntity
            {
                UserId = user.Id,
                CredentialId = passkeyInfo.CredentialId,
                PublicKey = passkeyInfo.PublicKey,
                CreatedAt = passkeyInfo.CreatedAt,
                SignCount = passkeyInfo.SignCount,
                Transports = passkeyInfo.Transports?.Length > 0
                    ? System.Text.Json.JsonSerializer.Serialize(passkeyInfo.Transports)
                    : null,
                IsUserVerified = passkeyInfo.IsUserVerified,
                IsBackupEligible = passkeyInfo.IsBackupEligible,
                IsBackedUp = passkeyInfo.IsBackedUp,
                AttestationObject = passkeyInfo.AttestationObject,
                ClientDataJson = passkeyInfo.ClientDataJson
            };
            UserPasskeys.Add(entity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemovePasskeyAsync(User user, byte[] credentialId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(credentialId);

        var passkey = await UserPasskeys
            .FirstOrDefaultAsync(up => up.UserId == user.Id && up.CredentialId == credentialId, cancellationToken);

        if (passkey != null)
        {
            UserPasskeys.Remove(passkey);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<User?> FindByPasskeyIdAsync(byte[] credentialId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(credentialId);

        var passkey = await UserPasskeys
            .FirstOrDefaultAsync(up => up.CredentialId == credentialId, cancellationToken);

        if (passkey == null) return null;

        var user = await Users.FindAsync([passkey.UserId], cancellationToken);
        if (user != null)
        {
            await LoadIdentityLinksAsync(user, cancellationToken);
        }
        return user;
    }

    #region Application.Identity.Interfaces.IUserStore Implementation

    async Task<User?> Application.Identity.Interfaces.IUserStore.FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await Users.FindAsync([id], cancellationToken);
        if (user != null)
        {
            await LoadIdentityLinksAsync(user, cancellationToken);
        }
        return user;
    }

    async Task<User?> Application.Identity.Interfaces.IUserStore.FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedUsername = username.ToUpperInvariant();
        var user = await Users
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUsername, cancellationToken);
        if (user != null)
        {
            await LoadIdentityLinksAsync(user, cancellationToken);
        }
        return user;
    }

    async Task<User?> Application.Identity.Interfaces.IUserStore.FindByExternalIdentityAsync(string provider, string providerSubject, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var login = await UserLogins
            .FirstOrDefaultAsync(ul => ul.LoginProvider == provider && ul.ProviderKey == providerSubject, cancellationToken);
        
        if (login == null) return null;

        var user = await Users.FindAsync([login.UserId], cancellationToken);
        if (user != null)
        {
            await LoadIdentityLinksAsync(user, cancellationToken);
        }
        return user;
    }

    async Task Application.Identity.Interfaces.IUserStore.SaveAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        var existingUser = await Users.FindAsync([user.Id], cancellationToken);
        if (existingUser == null)
        {
            Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _dbContext.Entry(existingUser).CurrentValues.SetValues(user);
        }
        // Save role assignments
        await SaveRoleAssignmentsAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    async Task<IReadOnlyCollection<User>> Application.Identity.Interfaces.IUserStore.ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var users = await Users.ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            await LoadIdentityLinksAsync(user, cancellationToken);
        }
        return users;
    }

    async Task<int> Application.Identity.Interfaces.IUserStore.DeleteAbandonedAnonymousUsersAsync(
        DateTimeOffset cutoffDate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Find anonymous users who have not been active since the cutoff date
        // Anonymous users have IsAnonymous = true
        // We check LastLoginAt if available, otherwise fall back to Created (from AuditableEntity)
        // Note: EF Core SQLite has issues translating nullable DateTimeOffset comparisons,
        // so we fetch anonymous users first, then filter in memory
        var anonymousUsers = await Users
            .Where(u => u.IsAnonymous)
            .ToListAsync(cancellationToken);
        
        var abandonedUsers = anonymousUsers
            .Where(u => (u.LastLoginAt.HasValue && u.LastLoginAt.Value < cutoffDate) ||
                       (!u.LastLoginAt.HasValue && u.Created < cutoffDate))
            .ToList();

        if (abandonedUsers.Count == 0)
        {
            return 0;
        }

        var userIds = abandonedUsers.Select(u => u.Id).ToList();

        // Remove related entities first (role assignments, passkeys, logins)
        var roleAssignments = await UserRoleAssignments
            .Where(ura => userIds.Contains(ura.UserId))
            .ToListAsync(cancellationToken);
        UserRoleAssignments.RemoveRange(roleAssignments);

        var passkeys = await UserPasskeys
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        UserPasskeys.RemoveRange(passkeys);

        var logins = await UserLogins
            .Where(l => userIds.Contains(l.UserId))
            .ToListAsync(cancellationToken);
        UserLogins.RemoveRange(logins);

        // Remove the users
        Users.RemoveRange(abandonedUsers);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return abandonedUsers.Count;
    }

    #endregion

    public void Dispose()
    {
        // DbContext is managed by DI
    }
}
