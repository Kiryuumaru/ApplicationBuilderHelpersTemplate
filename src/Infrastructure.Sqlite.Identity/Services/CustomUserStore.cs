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
    IUserLoginStore<User>
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
                TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount
            ) VALUES (
                @Id, @RevId, @UserName, @NormalizedUserName, @Email, @NormalizedEmail, @EmailConfirmed, 
                @PasswordHash, @SecurityStamp, @PhoneNumber, @PhoneNumberConfirmed, 
                @TwoFactorEnabled, @LockoutEnd, @LockoutEnabled, @AccessFailedCount
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
                TwoFactorEnabled = @TwoFactorEnabled, LockoutEnd = @LockoutEnd, 
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
        var email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email"));

        var constructor = typeof(User).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            new[] { typeof(Guid), typeof(string), typeof(string) },
            null);

        if (constructor == null) throw new InvalidOperationException("User constructor not found.");

        var user = (User)constructor.Invoke(new object?[] { id, userName, email });

        if (revId.HasValue)
        {
            user.RevId = revId.Value;
        }

        SetProp(user, "NormalizedUserName", reader.GetString(reader.GetOrdinal("NormalizedUserName")));
        SetProp(user, "NormalizedEmail", reader.IsDBNull(reader.GetOrdinal("NormalizedEmail")) ? null : reader.GetString(reader.GetOrdinal("NormalizedEmail")));
        SetProp(user, "EmailConfirmed", reader.GetBoolean(reader.GetOrdinal("EmailConfirmed")));
        SetProp(user, "PasswordHash", reader.IsDBNull(reader.GetOrdinal("PasswordHash")) ? null : reader.GetString(reader.GetOrdinal("PasswordHash")));
        SetProp(user, "SecurityStamp", reader.IsDBNull(reader.GetOrdinal("SecurityStamp")) ? null : reader.GetString(reader.GetOrdinal("SecurityStamp")));
        SetProp(user, "PhoneNumber", reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? null : reader.GetString(reader.GetOrdinal("PhoneNumber")));
        SetProp(user, "PhoneNumberConfirmed", reader.GetBoolean(reader.GetOrdinal("PhoneNumberConfirmed")));
        SetProp(user, "TwoFactorEnabled", reader.GetBoolean(reader.GetOrdinal("TwoFactorEnabled")));
        
        var lockoutEndStr = reader.IsDBNull(reader.GetOrdinal("LockoutEnd")) ? null : reader.GetString(reader.GetOrdinal("LockoutEnd"));
        if (lockoutEndStr != null) SetProp(user, "LockoutEnd", DateTimeOffset.Parse(lockoutEndStr));
        
        SetProp(user, "LockoutEnabled", reader.GetBoolean(reader.GetOrdinal("LockoutEnabled")));
        SetProp(user, "AccessFailedCount", reader.GetInt32(reader.GetOrdinal("AccessFailedCount")));

        return user;
    }

    private static void SetProp(object instance, string propName, object? value)
    {
        var prop = typeof(User).GetProperty(propName);
        prop?.SetValue(instance, value);
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
        // User.cs doesn't have SetPasswordHash method, need reflection or add it.
        // Assuming reflection for now as I can't modify Domain easily without breaking "Pure Domain" if I add setters everywhere.
        // But wait, User.cs has private setters.
        SetProp(user, "PasswordHash", passwordHash);
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
        var roleIdObj = await cmdRole.ExecuteScalarAsync(cancellationToken);
        if (roleIdObj == null) throw new InvalidOperationException($"Role {roleName} not found.");
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
        SetProp(user, "SecurityStamp", stamp);
        return Task.CompletedTask;
    }
    public Task<string?> GetSecurityStampAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnd);
    public Task SetLockoutEndDateAsync(User user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        // User.cs has private set.
        SetProp(user, "LockoutEnd", lockoutEnd);
        return Task.CompletedTask;
    }
    public Task<int> IncrementAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        // User.cs has private set.
        var count = user.AccessFailedCount + 1;
        SetProp(user, "AccessFailedCount", count);
        return Task.FromResult(count);
    }
    public Task ResetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        SetProp(user, "AccessFailedCount", 0);
        return Task.CompletedTask;
    }
    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnabled);
    public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        SetProp(user, "LockoutEnabled", enabled);
        return Task.CompletedTask;
    }

    public Task SetPhoneNumberAsync(User user, string? phoneNumber, CancellationToken cancellationToken)
    {
        SetProp(user, "PhoneNumber", phoneNumber);
        return Task.CompletedTask;
    }
    public Task<string?> GetPhoneNumberAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.PhoneNumber);
    public Task<bool> GetPhoneNumberConfirmedAsync(User user, CancellationToken cancellationToken) => Task.FromResult(user.PhoneNumberConfirmed);
    public Task SetPhoneNumberConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        SetProp(user, "PhoneNumberConfirmed", confirmed);
        return Task.CompletedTask;
    }

    public Task SetTwoFactorEnabledAsync(User user, bool enabled, CancellationToken cancellationToken)
    {
        SetProp(user, "TwoFactorEnabled", enabled);
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
    }    public void Dispose()
    {
    }
}
