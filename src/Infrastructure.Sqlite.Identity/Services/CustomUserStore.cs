using System.Security.Claims;
using Domain.Identity.Models;
using Domain.Identity.ValueObjects;
using Infrastructure.Sqlite.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace Infrastructure.Sqlite.Identity.Services;

public class CustomUserStore(SqliteConnectionFactory connectionFactory) : 
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
    IUserPasskeyStore<User>
{
    private readonly SqliteConnectionFactory _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Users (
                Id, RevId, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, 
                PasswordHash, SecurityStamp, PhoneNumber, PhoneNumberConfirmed, 
                TwoFactorEnabled, AuthenticatorKey, RecoveryCodes, LockoutEnd, LockoutEnabled, AccessFailedCount
            ) VALUES (
                @Id, @RevId, @UserName, @NormalizedUserName, @Email, @NormalizedEmail, @EmailConfirmed, 
                @PasswordHash, @SecurityStamp, @PhoneNumber, @PhoneNumberConfirmed, 
                @TwoFactorEnabled, @AuthenticatorKey, @RecoveryCodes, @LockoutEnd, @LockoutEnabled, @AccessFailedCount
            )";

        command.Parameters.AddWithValue("@Id", user.Id.ToString());
        command.Parameters.AddWithValue("@RevId", user.RevId.ToString());
        command.Parameters.AddWithValue("@UserName", user.UserName);
        command.Parameters.AddWithValue("@NormalizedUserName", user.NormalizedUserName);
        command.Parameters.AddWithValue("@Email", (object?)user.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("@NormalizedEmail", (object?)user.NormalizedEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("@EmailConfirmed", user.EmailConfirmed ? 1 : 0);
        command.Parameters.AddWithValue("@PasswordHash", (object?)user.PasswordHash ?? DBNull.Value);
        command.Parameters.AddWithValue("@SecurityStamp", (object?)user.SecurityStamp ?? DBNull.Value);
        command.Parameters.AddWithValue("@PhoneNumber", (object?)user.PhoneNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("@PhoneNumberConfirmed", user.PhoneNumberConfirmed ? 1 : 0);
        command.Parameters.AddWithValue("@TwoFactorEnabled", user.TwoFactorEnabled ? 1 : 0);
        command.Parameters.AddWithValue("@AuthenticatorKey", (object?)user.AuthenticatorKey ?? DBNull.Value);
        command.Parameters.AddWithValue("@RecoveryCodes", (object?)user.RecoveryCodes ?? DBNull.Value);
        command.Parameters.AddWithValue("@LockoutEnd", (object?)user.LockoutEnd?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("@LockoutEnabled", user.LockoutEnabled ? 1 : 0);
        command.Parameters.AddWithValue("@AccessFailedCount", user.AccessFailedCount);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return IdentityResult.Failed(new IdentityError { Description = "User already exists." });
        }

        // Save Permissions
        foreach (var perm in user.PermissionGrants)
        {
            using var cmdPerm = connection.CreateCommand();
            cmdPerm.CommandText = "INSERT INTO UserPermissions (UserId, PermissionIdentifier) VALUES (@UserId, @PermissionIdentifier)";
            cmdPerm.Parameters.AddWithValue("@UserId", user.Id.ToString());
            cmdPerm.Parameters.AddWithValue("@PermissionIdentifier", perm.Identifier);
            await cmdPerm.ExecuteNonQueryAsync(cancellationToken);
        }

        // Save Roles
        foreach (var role in user.RoleAssignments)
        {
            using var cmdRole = connection.CreateCommand();
            cmdRole.CommandText = "INSERT INTO UserRoles (UserId, RoleId, ParameterValues) VALUES (@UserId, @RoleId, @ParameterValues)";
            cmdRole.Parameters.AddWithValue("@UserId", user.Id.ToString());
            cmdRole.Parameters.AddWithValue("@RoleId", role.RoleId.ToString());
            cmdRole.Parameters.AddWithValue("@ParameterValues", System.Text.Json.JsonSerializer.Serialize(role.ParameterValues));
            await cmdRole.ExecuteNonQueryAsync(cancellationToken);
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = @"
            UPDATE Users SET
                RevId = @RevId, UserName = @UserName, NormalizedUserName = @NormalizedUserName, 
                Email = @Email, NormalizedEmail = @NormalizedEmail, EmailConfirmed = @EmailConfirmed, 
                PasswordHash = @PasswordHash, SecurityStamp = @SecurityStamp, 
                PhoneNumber = @PhoneNumber, PhoneNumberConfirmed = @PhoneNumberConfirmed, 
                TwoFactorEnabled = @TwoFactorEnabled, AuthenticatorKey = @AuthenticatorKey, 
                RecoveryCodes = @RecoveryCodes, LockoutEnd = @LockoutEnd, 
                LockoutEnabled = @LockoutEnabled, AccessFailedCount = @AccessFailedCount
            WHERE Id = @Id";

        command.Parameters.AddWithValue("@Id", user.Id.ToString());
        command.Parameters.AddWithValue("@RevId", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("@UserName", user.UserName);
        command.Parameters.AddWithValue("@NormalizedUserName", user.NormalizedUserName);
        command.Parameters.AddWithValue("@Email", (object?)user.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("@NormalizedEmail", (object?)user.NormalizedEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("@EmailConfirmed", user.EmailConfirmed ? 1 : 0);
        command.Parameters.AddWithValue("@PasswordHash", (object?)user.PasswordHash ?? DBNull.Value);
        command.Parameters.AddWithValue("@SecurityStamp", (object?)user.SecurityStamp ?? DBNull.Value);
        command.Parameters.AddWithValue("@PhoneNumber", (object?)user.PhoneNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("@PhoneNumberConfirmed", user.PhoneNumberConfirmed ? 1 : 0);
        command.Parameters.AddWithValue("@TwoFactorEnabled", user.TwoFactorEnabled ? 1 : 0);
        command.Parameters.AddWithValue("@AuthenticatorKey", (object?)user.AuthenticatorKey ?? DBNull.Value);
        command.Parameters.AddWithValue("@RecoveryCodes", (object?)user.RecoveryCodes ?? DBNull.Value);
        command.Parameters.AddWithValue("@LockoutEnd", (object?)user.LockoutEnd?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("@LockoutEnabled", user.LockoutEnabled ? 1 : 0);
        command.Parameters.AddWithValue("@AccessFailedCount", user.AccessFailedCount);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
        {
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });
        }

        // Update Permissions
        using (var cmdDelPerm = connection.CreateCommand())
        {
            cmdDelPerm.CommandText = "DELETE FROM UserPermissions WHERE UserId = @UserId";
            cmdDelPerm.Parameters.AddWithValue("@UserId", user.Id.ToString());
            await cmdDelPerm.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var perm in user.PermissionGrants)
        {
            using var cmdPerm = connection.CreateCommand();
            cmdPerm.CommandText = "INSERT INTO UserPermissions (UserId, PermissionIdentifier) VALUES (@UserId, @PermissionIdentifier)";
            cmdPerm.Parameters.AddWithValue("@UserId", user.Id.ToString());
            cmdPerm.Parameters.AddWithValue("@PermissionIdentifier", perm.Identifier);
            await cmdPerm.ExecuteNonQueryAsync(cancellationToken);
        }

        // Update Roles
        using (var cmdDelRole = connection.CreateCommand())
        {
            cmdDelRole.CommandText = "DELETE FROM UserRoles WHERE UserId = @UserId";
            cmdDelRole.Parameters.AddWithValue("@UserId", user.Id.ToString());
            await cmdDelRole.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var role in user.RoleAssignments)
        {
            using var cmdRole = connection.CreateCommand();
            cmdRole.CommandText = "INSERT INTO UserRoles (UserId, RoleId, ParameterValues) VALUES (@UserId, @RoleId, @ParameterValues)";
            cmdRole.Parameters.AddWithValue("@UserId", user.Id.ToString());
            cmdRole.Parameters.AddWithValue("@RoleId", role.RoleId.ToString());
            cmdRole.Parameters.AddWithValue("@ParameterValues", System.Text.Json.JsonSerializer.Serialize(role.ParameterValues));
            await cmdRole.ExecuteNonQueryAsync(cancellationToken);
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = "DELETE FROM Users WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", user.Id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var guid)) return null;

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        User? user = null;
        using (var command = (SqliteCommand)connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM Users WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", userId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                user = HydrateUser(reader);
            }
        }
        
        if (user != null) await LoadRelatedDataAsync(connection, user, cancellationToken);
        return user;
    }

    public async Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        User? user = null;
        using (var command = (SqliteCommand)connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM Users WHERE NormalizedUserName = @NormalizedUserName";
            command.Parameters.AddWithValue("@NormalizedUserName", normalizedUserName);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                user = HydrateUser(reader);
            }
        }
        
        if (user != null) await LoadRelatedDataAsync(connection, user, cancellationToken);
        return user;
    }

    private static User HydrateUser(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id")));
        var revId = reader.IsDBNull(reader.GetOrdinal("RevId")) ? (Guid?)null : Guid.Parse(reader.GetString(reader.GetOrdinal("RevId")));
        var userName = reader.GetString(reader.GetOrdinal("UserName"));
        var normalizedUserName = reader.GetString(reader.GetOrdinal("NormalizedUserName"));
        var email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email"));
        var normalizedEmail = reader.IsDBNull(reader.GetOrdinal("NormalizedEmail")) ? null : reader.GetString(reader.GetOrdinal("NormalizedEmail"));
        var emailConfirmed = reader.GetBoolean(reader.GetOrdinal("EmailConfirmed"));
        var passwordHash = reader.IsDBNull(reader.GetOrdinal("PasswordHash")) ? null : reader.GetString(reader.GetOrdinal("PasswordHash"));
        var securityStamp = reader.IsDBNull(reader.GetOrdinal("SecurityStamp")) ? null : reader.GetString(reader.GetOrdinal("SecurityStamp"));
        var phoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? null : reader.GetString(reader.GetOrdinal("PhoneNumber"));
        var phoneNumberConfirmed = reader.GetBoolean(reader.GetOrdinal("PhoneNumberConfirmed"));
        var twoFactorEnabled = reader.GetBoolean(reader.GetOrdinal("TwoFactorEnabled"));
        var authenticatorKey = reader.IsDBNull(reader.GetOrdinal("AuthenticatorKey")) ? null : reader.GetString(reader.GetOrdinal("AuthenticatorKey"));
        var recoveryCodes = reader.IsDBNull(reader.GetOrdinal("RecoveryCodes")) ? null : reader.GetString(reader.GetOrdinal("RecoveryCodes"));
        var lockoutEndStr = reader.IsDBNull(reader.GetOrdinal("LockoutEnd")) ? null : reader.GetString(reader.GetOrdinal("LockoutEnd"));
        var lockoutEnd = lockoutEndStr != null ? DateTimeOffset.Parse(lockoutEndStr) : (DateTimeOffset?)null;
        var lockoutEnabled = reader.GetBoolean(reader.GetOrdinal("LockoutEnabled"));
        var accessFailedCount = reader.GetInt32(reader.GetOrdinal("AccessFailedCount"));

        return User.Hydrate(
            id,
            revId,
            userName,
            normalizedUserName,
            email,
            normalizedEmail,
            emailConfirmed,
            passwordHash,
            securityStamp,
            phoneNumber,
            phoneNumberConfirmed,
            twoFactorEnabled,
            authenticatorKey,
            recoveryCodes,
            lockoutEnd,
            lockoutEnabled,
            accessFailedCount);
    }

    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName);
    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken)
    {
        if (userName != null) user.SetUserName(userName);
        return Task.CompletedTask;
    }
    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken)
    {
        if (normalizedName != null) user.SetNormalizedUserName(normalizedName);
        return Task.CompletedTask;
    }

    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.SetPasswordHash(passwordHash);
        return Task.CompletedTask;
    }
    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash);
    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash != null);

    public Task SetEmailAsync(User user, string? email, CancellationToken cancellationToken)
    {
        user.SetEmail(email);
        return Task.CompletedTask;
    }
    public Task<string?> GetEmailAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.Email);
    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.EmailConfirmed);
    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        user.SetEmailConfirmed(confirmed);
        return Task.CompletedTask;
    }
    public async Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        User? user = null;
        using (var command = (SqliteCommand)connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM Users WHERE NormalizedEmail = @NormalizedEmail";
            command.Parameters.AddWithValue("@NormalizedEmail", normalizedEmail);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                user = HydrateUser(reader);
            }
        }
        
        if (user != null) await LoadRelatedDataAsync(connection, user, cancellationToken);
        return user;
    }
    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);
    public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.SetNormalizedEmail(normalizedEmail);
        return Task.CompletedTask;
    }

    public async Task AddToRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        // Need to find RoleId by RoleName
        // This store doesn't know about RoleStore directly.
        // But I can query Roles table.
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        
        // Find Role ID
        using var cmdRole = (SqliteCommand)connection.CreateCommand();
        cmdRole.CommandText = "SELECT Id FROM Roles WHERE NormalizedName = @NormalizedName";
        cmdRole.Parameters.AddWithValue("@NormalizedName", roleName.ToUpperInvariant());
        var roleIdObj = await cmdRole.ExecuteScalarAsync(cancellationToken) ?? throw new InvalidOperationException($"Role {roleName} not found.");
        var roleId = roleIdObj.ToString()!;

        // Insert UserRole
        using var cmdInsert = (SqliteCommand)connection.CreateCommand();
        cmdInsert.CommandText = "INSERT INTO UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)";
        cmdInsert.Parameters.AddWithValue("@UserId", user.Id.ToString());
        cmdInsert.Parameters.AddWithValue("@RoleId", roleId);
        try
        {
            await cmdInsert.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { } // Ignore if exists

        // Update in-memory user object
        var rolesField = typeof(User).GetField("_roleAssignments", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var rolesSet = (HashSet<UserRoleAssignment>)rolesField!.GetValue(user)!;
        rolesSet.Add(UserRoleAssignment.Create(Guid.Parse(roleId)));
    }

    public async Task RemoveFromRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        
        // Find Role ID
        using var cmdRole = (SqliteCommand)connection.CreateCommand();
        cmdRole.CommandText = "SELECT Id FROM Roles WHERE NormalizedName = @NormalizedName";
        cmdRole.Parameters.AddWithValue("@NormalizedName", roleName.ToUpperInvariant());
        var roleIdObj = await cmdRole.ExecuteScalarAsync(cancellationToken);
        if (roleIdObj == null) return; // Role doesn't exist, so user can't be in it.
        var roleId = roleIdObj.ToString();

        using var cmdDelete = (SqliteCommand)connection.CreateCommand();
        cmdDelete.CommandText = "DELETE FROM UserRoles WHERE UserId = @UserId AND RoleId = @RoleId";
        cmdDelete.Parameters.AddWithValue("@UserId", user.Id.ToString());
        cmdDelete.Parameters.AddWithValue("@RoleId", roleId);
        await cmdDelete.ExecuteNonQueryAsync(cancellationToken);

        // Update in-memory user object
        var rolesField = typeof(User).GetField("_roleAssignments", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var rolesSet = (HashSet<UserRoleAssignment>)rolesField!.GetValue(user)!;
        var assignmentToRemove = rolesSet.FirstOrDefault(r => r.RoleId.ToString() == roleId);
        if (assignmentToRemove != null)
        {
            rolesSet.Remove(assignmentToRemove);
        }
    }

    public async Task<IList<string>> GetRolesAsync(User user, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = @"
            SELECT r.Name 
            FROM Roles r 
            INNER JOIN UserRoles ur ON r.Id = ur.RoleId 
            WHERE ur.UserId = @UserId";
        command.Parameters.AddWithValue("@UserId", user.Id.ToString());

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var roles = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(reader.GetString(0));
        }
        return roles;
    }

    public async Task<bool> IsInRoleAsync(User user, string roleName, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = @"
            SELECT 1 
            FROM Roles r 
            INNER JOIN UserRoles ur ON r.Id = ur.RoleId 
            WHERE ur.UserId = @UserId AND r.NormalizedName = @NormalizedName";
        command.Parameters.AddWithValue("@UserId", user.Id.ToString());
        command.Parameters.AddWithValue("@NormalizedName", roleName.ToUpperInvariant());

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    public async Task<IList<User>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = @"
            SELECT u.* 
            FROM Users u 
            INNER JOIN UserRoles ur ON u.Id = ur.UserId 
            INNER JOIN Roles r ON ur.RoleId = r.Id 
            WHERE r.NormalizedName = @NormalizedName";
        command.Parameters.AddWithValue("@NormalizedName", roleName.ToUpperInvariant());

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<User>();
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(HydrateUser(reader));
        }
        return users;
    }

    public Task SetSecurityStampAsync(User user, string stamp, CancellationToken cancellationToken)
    {
        user.SetSecurityStamp(stamp);
        return Task.CompletedTask;
    }
    public Task<string?> GetSecurityStampAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnd);
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
    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnabled);
    public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        user.SetLockoutEnabled(enabled);
        return Task.CompletedTask;
    }

    public Task SetPhoneNumberAsync(User user, string? phoneNumber, CancellationToken cancellationToken)
    {
        user.SetPhoneNumber(phoneNumber);
        return Task.CompletedTask;
    }
    public Task<string?> GetPhoneNumberAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.PhoneNumber);
    public Task<bool> GetPhoneNumberConfirmedAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.PhoneNumberConfirmed);
    public Task SetPhoneNumberConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        user.SetPhoneNumberConfirmed(confirmed);
        return Task.CompletedTask;
    }

    public Task SetTwoFactorEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        user.SetTwoFactorEnabled(enabled);
        return Task.CompletedTask;
    }
    public Task<bool> GetTwoFactorEnabledAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.TwoFactorEnabled);

    public async Task AddLoginAsync(User user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO UserLogins (LoginProvider, ProviderKey, ProviderDisplayName, UserId)
            VALUES (@LoginProvider, @ProviderKey, @ProviderDisplayName, @UserId)";
        command.Parameters.AddWithValue("@LoginProvider", login.LoginProvider);
        command.Parameters.AddWithValue("@ProviderKey", login.ProviderKey);
        command.Parameters.AddWithValue("@ProviderDisplayName", (object?)login.ProviderDisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("@UserId", user.Id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveLoginAsync(User user, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM UserLogins 
            WHERE LoginProvider = @LoginProvider AND ProviderKey = @ProviderKey AND UserId = @UserId";
        command.Parameters.AddWithValue("@LoginProvider", loginProvider);
        command.Parameters.AddWithValue("@ProviderKey", providerKey);
        command.Parameters.AddWithValue("@UserId", user.Id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(User user, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = "SELECT LoginProvider, ProviderKey, ProviderDisplayName FROM UserLogins WHERE UserId = @UserId";
        command.Parameters.AddWithValue("@UserId", user.Id.ToString());

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var logins = new List<UserLoginInfo>();
        while (await reader.ReadAsync(cancellationToken))
        {
            logins.Add(new UserLoginInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)
            ));
        }
        return logins;
    }

    public async Task<User?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        User? user = null;
        using (var command = (SqliteCommand)connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT u.* 
                FROM Users u 
                INNER JOIN UserLogins ul ON u.Id = ul.UserId 
                WHERE ul.LoginProvider = @LoginProvider AND ul.ProviderKey = @ProviderKey";
            command.Parameters.AddWithValue("@LoginProvider", loginProvider);
            command.Parameters.AddWithValue("@ProviderKey", providerKey);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                user = HydrateUser(reader);
            }
        }
        
        if (user != null) await LoadRelatedDataAsync(connection, user, cancellationToken);
        return user;
    }

    private async Task LoadRelatedDataAsync(SqliteConnection connection, User user, CancellationToken cancellationToken)
    {
        // Load Permissions
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT PermissionIdentifier FROM UserPermissions WHERE UserId = @UserId";
            command.Parameters.AddWithValue("@UserId", user.Id.ToString());
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var permissions = new List<UserPermissionGrant>();
            while (await reader.ReadAsync(cancellationToken))
            {
                permissions.Add(UserPermissionGrant.Create(reader.GetString(0)));
            }
            
             var grantsField = typeof(User).GetField("_permissionGrants", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
             var grantsSet = (HashSet<UserPermissionGrant>)grantsField!.GetValue(user)!;
             grantsSet.Clear();
             foreach(var p in permissions) grantsSet.Add(p);
        }

        // Load Roles
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT RoleId, ParameterValues FROM UserRoles WHERE UserId = @UserId";
            command.Parameters.AddWithValue("@UserId", user.Id.ToString());
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var roles = new List<UserRoleAssignment>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var roleId = Guid.Parse(reader.GetString(0));
                var paramsJson = reader.IsDBNull(1) ? "{}" : reader.GetString(1);
                var parameters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(paramsJson);
                roles.Add(UserRoleAssignment.Create(roleId, parameters));
            }
            
             var rolesField = typeof(User).GetField("_roleAssignments", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
             var rolesSet = (HashSet<UserRoleAssignment>)rolesField!.GetValue(user)!;
             rolesSet.Clear();
             foreach(var r in roles) rolesSet.Add(r);
        }

        // Load Logins
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT LoginProvider, ProviderKey, ProviderDisplayName FROM UserLogins WHERE UserId = @UserId";
            command.Parameters.AddWithValue("@UserId", user.Id.ToString());
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var links = new List<UserIdentityLink>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var provider = reader.GetString(0);
                var key = reader.GetString(1);
                var displayName = reader.IsDBNull(2) ? null : reader.GetString(2);
                links.Add(UserIdentityLink.Create(provider, key, null, displayName));
            }
            
             var linksField = typeof(User).GetField("_identityLinks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
             var linksDict = (Dictionary<string, UserIdentityLink>)linksField!.GetValue(user)!;
             linksDict.Clear();
             foreach(var l in links) linksDict[l.Provider] = l;
        }
    }

    // IUserPasskeyStore implementation
    public async Task<IList<UserPasskeyInfo>> GetPasskeysAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT CredentialId, PublicKey, Name, CreatedAt, SignCount, Transports, 
                   IsUserVerified, IsBackupEligible, IsBackedUp, AttestationObject, ClientDataJson
            FROM UserPasskeys WHERE UserId = @UserId";
        command.Parameters.AddWithValue("@UserId", user.Id.ToString());

        var passkeys = new List<UserPasskeyInfo>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var credentialId = (byte[])reader["CredentialId"];
            var publicKey = reader.IsDBNull(reader.GetOrdinal("PublicKey")) ? Array.Empty<byte>() : (byte[])reader["PublicKey"];
            var createdAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")));
            var signCount = (uint)reader.GetInt32(reader.GetOrdinal("SignCount"));
            var transportsJson = reader.IsDBNull(reader.GetOrdinal("Transports")) ? null : reader.GetString(reader.GetOrdinal("Transports"));
            var isUserVerified = reader.GetBoolean(reader.GetOrdinal("IsUserVerified"));
            var isBackupEligible = reader.GetBoolean(reader.GetOrdinal("IsBackupEligible"));
            var isBackedUp = reader.GetBoolean(reader.GetOrdinal("IsBackedUp"));
            var attestationObject = reader.IsDBNull(reader.GetOrdinal("AttestationObject")) ? Array.Empty<byte>() : (byte[])reader["AttestationObject"];
            var clientDataJson = reader.IsDBNull(reader.GetOrdinal("ClientDataJson")) ? Array.Empty<byte>() : (byte[])reader["ClientDataJson"];

            var transports = string.IsNullOrEmpty(transportsJson) 
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<string[]>(transportsJson);

            passkeys.Add(new UserPasskeyInfo(
                credentialId,
                publicKey,
                createdAt,
                signCount,
                transports,
                isUserVerified,
                isBackupEligible,
                isBackedUp,
                attestationObject,
                clientDataJson
            ));
        }

        return passkeys;
    }

    public async Task<UserPasskeyInfo?> FindPasskeyAsync(User user, byte[] credentialId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(credentialId);

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT CredentialId, PublicKey, Name, CreatedAt, SignCount, Transports, 
                   IsUserVerified, IsBackupEligible, IsBackedUp, AttestationObject, ClientDataJson
            FROM UserPasskeys WHERE UserId = @UserId AND CredentialId = @CredentialId";
        command.Parameters.AddWithValue("@UserId", user.Id.ToString());
        command.Parameters.AddWithValue("@CredentialId", credentialId);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var cId = (byte[])reader["CredentialId"];
            var publicKey = reader.IsDBNull(reader.GetOrdinal("PublicKey")) ? Array.Empty<byte>() : (byte[])reader["PublicKey"];
            var createdAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")));
            var signCount = (uint)reader.GetInt32(reader.GetOrdinal("SignCount"));
            var transportsJson = reader.IsDBNull(reader.GetOrdinal("Transports")) ? null : reader.GetString(reader.GetOrdinal("Transports"));
            var isUserVerified = reader.GetBoolean(reader.GetOrdinal("IsUserVerified"));
            var isBackupEligible = reader.GetBoolean(reader.GetOrdinal("IsBackupEligible"));
            var isBackedUp = reader.GetBoolean(reader.GetOrdinal("IsBackedUp"));
            var attestationObject = reader.IsDBNull(reader.GetOrdinal("AttestationObject")) ? Array.Empty<byte>() : (byte[])reader["AttestationObject"];
            var clientDataJson = reader.IsDBNull(reader.GetOrdinal("ClientDataJson")) ? Array.Empty<byte>() : (byte[])reader["ClientDataJson"];

            var transports = string.IsNullOrEmpty(transportsJson) 
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<string[]>(transportsJson);

            return new UserPasskeyInfo(
                cId,
                publicKey,
                createdAt,
                signCount,
                transports,
                isUserVerified,
                isBackupEligible,
                isBackedUp,
                attestationObject,
                clientDataJson
            );
        }

        return null;
    }

    public async Task AddOrUpdatePasskeyAsync(User user, UserPasskeyInfo passkeyInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(passkeyInfo);

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO UserPasskeys (UserId, CredentialId, PublicKey, Name, CreatedAt, SignCount, 
                                      Transports, IsUserVerified, IsBackupEligible, IsBackedUp, 
                                      AttestationObject, ClientDataJson)
            VALUES (@UserId, @CredentialId, @PublicKey, @Name, @CreatedAt, @SignCount, 
                    @Transports, @IsUserVerified, @IsBackupEligible, @IsBackedUp, 
                    @AttestationObject, @ClientDataJson)
            ON CONFLICT(UserId, CredentialId) DO UPDATE SET
                PublicKey = @PublicKey, Name = @Name, SignCount = @SignCount, 
                Transports = @Transports, IsUserVerified = @IsUserVerified, 
                IsBackupEligible = @IsBackupEligible, IsBackedUp = @IsBackedUp,
                AttestationObject = @AttestationObject, ClientDataJson = @ClientDataJson";

        command.Parameters.AddWithValue("@UserId", user.Id.ToString());
        command.Parameters.AddWithValue("@CredentialId", passkeyInfo.CredentialId);
        command.Parameters.AddWithValue("@PublicKey", (object?)passkeyInfo.PublicKey ?? DBNull.Value);
        command.Parameters.AddWithValue("@Name", DBNull.Value); // Name is not part of UserPasskeyInfo
        command.Parameters.AddWithValue("@CreatedAt", passkeyInfo.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@SignCount", (int)passkeyInfo.SignCount);
        command.Parameters.AddWithValue("@Transports", passkeyInfo.Transports?.Length > 0 
            ? System.Text.Json.JsonSerializer.Serialize(passkeyInfo.Transports) 
            : DBNull.Value);
        command.Parameters.AddWithValue("@IsUserVerified", passkeyInfo.IsUserVerified ? 1 : 0);
        command.Parameters.AddWithValue("@IsBackupEligible", passkeyInfo.IsBackupEligible ? 1 : 0);
        command.Parameters.AddWithValue("@IsBackedUp", passkeyInfo.IsBackedUp ? 1 : 0);
        command.Parameters.AddWithValue("@AttestationObject", (object?)passkeyInfo.AttestationObject ?? DBNull.Value);
        command.Parameters.AddWithValue("@ClientDataJson", (object?)passkeyInfo.ClientDataJson ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemovePasskeyAsync(User user, byte[] credentialId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(credentialId);

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM UserPasskeys WHERE UserId = @UserId AND CredentialId = @CredentialId";
        command.Parameters.AddWithValue("@UserId", user.Id.ToString());
        command.Parameters.AddWithValue("@CredentialId", credentialId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<User?> FindByPasskeyIdAsync(byte[] credentialId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(credentialId);

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT UserId FROM UserPasskeys WHERE CredentialId = @CredentialId";
        command.Parameters.AddWithValue("@CredentialId", credentialId);

        var userId = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (userId == null) return null;

        return await FindByIdAsync(userId, cancellationToken);
    }

    #region IUserAuthenticatorKeyStore

    public Task SetAuthenticatorKeyAsync(User user, string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        user.SetAuthenticatorKey(key);
        return Task.CompletedTask;
    }

    public Task<string?> GetAuthenticatorKeyAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.AuthenticatorKey);
    }

    #endregion

    #region IUserTwoFactorRecoveryCodeStore

    public Task ReplaceCodesAsync(User user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(recoveryCodes);
        
        // Store recovery codes as semicolon-separated string
        user.SetRecoveryCodes(string.Join(";", recoveryCodes));
        return Task.CompletedTask;
    }

    public Task<bool> RedeemCodeAsync(User user, string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(code);

        var codes = user.RecoveryCodes?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (codes == null || codes.Count == 0)
        {
            return Task.FromResult(false);
        }

        if (codes.Contains(code))
        {
            codes.Remove(code);
            user.SetRecoveryCodes(codes.Count > 0 ? string.Join(";", codes) : null);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<int> CountCodesAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        var codes = user.RecoveryCodes?.Split(';', StringSplitOptions.RemoveEmptyEntries);
        return Task.FromResult(codes?.Length ?? 0);
    }

    #endregion

    public void Dispose()
    {
    }
}
