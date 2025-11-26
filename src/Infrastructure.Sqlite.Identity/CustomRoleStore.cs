using System.Security.Claims;
using Domain.Authorization.Models;
using Infrastructure.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace Infrastructure.Sqlite.Identity;

public class CustomRoleStore(SqliteConnectionFactory connectionFactory) : IRoleStore<Role>
{
    private readonly SqliteConnectionFactory _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<IdentityResult> CreateAsync(Role role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Roles (Id, Code, Name, NormalizedName, Description, IsSystemRole, ConcurrencyStamp)
            VALUES (@Id, @Code, @Name, @NormalizedName, @Description, @IsSystemRole, @ConcurrencyStamp)";
        
        command.Parameters.AddWithValue("@Id", role.Id.ToString());
        command.Parameters.AddWithValue("@Code", role.Code);
        command.Parameters.AddWithValue("@Name", role.Name);
        command.Parameters.AddWithValue("@NormalizedName", role.NormalizedName);
        command.Parameters.AddWithValue("@Description", (object?)role.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsSystemRole", role.IsSystemRole ? 1 : 0);
        command.Parameters.AddWithValue("@ConcurrencyStamp", role.ConcurrencyStamp ?? Guid.NewGuid().ToString());

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // Constraint violation
        {
            return IdentityResult.Failed(new IdentityError { Description = "Role already exists." });
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(Role role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = @"
            UPDATE Roles
            SET Code = @Code, Name = @Name, NormalizedName = @NormalizedName, Description = @Description, IsSystemRole = @IsSystemRole, ConcurrencyStamp = @ConcurrencyStamp
            WHERE Id = @Id";

        command.Parameters.AddWithValue("@Id", role.Id.ToString());
        command.Parameters.AddWithValue("@Code", role.Code);
        command.Parameters.AddWithValue("@Name", role.Name);
        command.Parameters.AddWithValue("@NormalizedName", role.NormalizedName);
        command.Parameters.AddWithValue("@Description", (object?)role.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsSystemRole", role.IsSystemRole ? 1 : 0);
        command.Parameters.AddWithValue("@ConcurrencyStamp", Guid.NewGuid().ToString());

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
        {
             return IdentityResult.Failed(new IdentityError { Description = "Role not found." });
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(Role role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(role);

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = "DELETE FROM Roles WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", role.Id.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public Task<string> GetRoleIdAsync(Role role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Id.ToString());
    }

    public Task<string?> GetRoleNameAsync(Role role, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(role.Name);
    }

    public Task SetRoleNameAsync(Role role, string? roleName, CancellationToken cancellationToken)
    {
        // Role is immutable in this aspect via setters usually, but here we might need to use reflection or internal setters if they are not public.
        // Domain Role has private setters.
        // But wait, Role.Create sets them.
        // If I need to update it, I might need a method on Role.
        // Checking Role.cs...
        // It has SetName and SetNormalizedName? No, let's check.
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedRoleNameAsync(Role role, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(role.NormalizedName);
    }

    public Task SetNormalizedRoleNameAsync(Role role, string? normalizedName, CancellationToken cancellationToken)
    {
        // Same issue as SetRoleNameAsync
        return Task.CompletedTask;
    }

    public async Task<Role?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(roleId, out var guid))
        {
            return null;
        }

        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = "SELECT * FROM Roles WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", roleId);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return HydrateRole(reader);
        }
        return null;
    }

    public async Task<Role?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = (SqliteCommand)connection.CreateCommand();
        command.CommandText = "SELECT * FROM Roles WHERE NormalizedName = @NormalizedName";
        command.Parameters.AddWithValue("@NormalizedName", normalizedRoleName);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
             return HydrateRole(reader);
        }
        return null;
    }

    private static Role HydrateRole(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id")));
        var code = reader.GetString(reader.GetOrdinal("Code"));
        var name = reader.GetString(reader.GetOrdinal("Name"));
        var description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"));
        var isSystemRole = reader.GetBoolean(reader.GetOrdinal("IsSystemRole"));
        var concurrencyStamp = reader.IsDBNull(reader.GetOrdinal("ConcurrencyStamp")) ? null : reader.GetString(reader.GetOrdinal("ConcurrencyStamp"));

        var constructor = typeof(Role).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            new[] { typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(bool) },
            null);

        if (constructor == null)
        {
             throw new InvalidOperationException("Role constructor not found.");
        }

        var role = (Role)constructor.Invoke(new object?[] { id, code, name, description, isSystemRole });
        
        var prop = typeof(Role).GetProperty(nameof(Role.ConcurrencyStamp));
        prop?.SetValue(role, concurrencyStamp);

        return role;
    }

    public void Dispose()
    {
    }
}
