using System.Data;
using System.Text.Json;
using Application.Authorization.Roles.Interfaces;
using Domain.Authorization.Models;
using Domain.Authorization.ValueObjects;
using Infrastructure.Sqlite.Services;
using Microsoft.Data.Sqlite;

namespace Infrastructure.Sqlite.Identity.Services;

public sealed class SqliteRoleRepository(SqliteConnectionFactory connectionFactory) : IRoleRepository, IRoleLookup
{
    private readonly SqliteConnectionFactory _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public Role? FindById(Guid id)
    {
        using var connection = (SqliteConnection)_connectionFactory.CreateConnection();
        connection.Open();
        return GetRoleBase(connection, "Id", id.ToString());
    }

    public IReadOnlyCollection<Role> GetByIds(IEnumerable<Guid> ids)
    {
        var list = new List<Role>();
        using var connection = (SqliteConnection)_connectionFactory.CreateConnection();
        connection.Open();
        
        foreach (var id in ids)
        {
            var role = GetRoleBase(connection, "Id", id.ToString());
            if (role != null) list.Add(role);
        }
        return list;
    }

    private Role? GetRoleBase(SqliteConnection connection, string column, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT Id, Code, Name, Description, IsSystemRole, ConcurrencyStamp
            FROM Roles
            WHERE {column} = @Value";
        command.Parameters.AddWithValue("@Value", value);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            var id = Guid.Parse(reader.GetString(0));
            var code = reader.GetString(1);
            var name = reader.GetString(2);
            var description = reader.IsDBNull(3) ? null : reader.GetString(3);
            var isSystemRole = reader.GetBoolean(4);
            var concurrencyStamp = reader.GetString(5);

            var role = (Role)Activator.CreateInstance(
                typeof(Role), 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, 
                null, 
                new object?[] { id, code, name, description, isSystemRole }, 
                null)!;
            
            typeof(Role).GetProperty(nameof(Role.ConcurrencyStamp))!.SetValue(role, concurrencyStamp);
            
            LoadPermissions(connection, role);

            return role;
        }
        return null;
    }

    private void LoadPermissions(SqliteConnection connection, Role role)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT IdentifierTemplate, Description, RequiredParameters FROM RolePermissions WHERE RoleId = @RoleId";
        command.Parameters.AddWithValue("@RoleId", role.Id.ToString());

        using var reader = command.ExecuteReader();
        var templates = new List<RolePermissionTemplate>();
        while (reader.Read())
        {
            var template = reader.GetString(0);
            var description = reader.IsDBNull(1) ? null : reader.GetString(1);
            var requiredParamsJson = reader.IsDBNull(2) ? "[]" : reader.GetString(2);
            var requiredParams = JsonSerializer.Deserialize<List<string>>(requiredParamsJson);

            templates.Add(RolePermissionTemplate.Create(template, requiredParams, description));
        }
        
        role.ReplacePermissions(templates);
    }

    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        var role = await GetRoleAsync(connection, "Id", id.ToString(), cancellationToken);
        if (role == null) throw new Exception($"DEBUG: Role not found for ID {id}");
        return role;
    }

    public async Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        return await GetRoleAsync(connection, "Code", code.Trim().ToUpperInvariant(), cancellationToken);
    }

    private async Task<Role?> GetRoleAsync(SqliteConnection connection, string column, string value, CancellationToken cancellationToken)
    {
        var role = await GetRoleBaseAsync(connection, column, value, cancellationToken);
        if (role is null) return null;

        await LoadPermissionsAsync(connection, role, cancellationToken);
        return role;
    }

    private async Task<Role?> GetRoleBaseAsync(SqliteConnection connection, string column, string value, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT Id, Code, Name, Description, IsSystemRole, ConcurrencyStamp
            FROM Roles
            WHERE {column} = @Value";
        command.Parameters.AddWithValue("@Value", value);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var id = Guid.Parse(reader.GetString(0));
            var code = reader.GetString(1);
            var name = reader.GetString(2);
            var description = reader.IsDBNull(3) ? null : reader.GetString(3);
            var isSystemRole = reader.GetBoolean(4);
            var concurrencyStamp = reader.GetString(5);

            var role = (Role)Activator.CreateInstance(
                typeof(Role), 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, 
                null, 
                new object?[] { id, code, name, description, isSystemRole }, 
                null)!;
            
            typeof(Role).GetProperty(nameof(Role.ConcurrencyStamp))!.SetValue(role, concurrencyStamp);
            
            return role;
        }
        return null;
    }

    private async Task LoadPermissionsAsync(SqliteConnection connection, Role role, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT IdentifierTemplate, Description, RequiredParameters FROM RolePermissions WHERE RoleId = @RoleId";
        command.Parameters.AddWithValue("@RoleId", role.Id.ToString());

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var templates = new List<RolePermissionTemplate>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var template = reader.GetString(0);
            var description = reader.IsDBNull(1) ? null : reader.GetString(1);
            var requiredParamsJson = reader.IsDBNull(2) ? "[]" : reader.GetString(2);
            var requiredParams = JsonSerializer.Deserialize<List<string>>(requiredParamsJson);

            templates.Add(RolePermissionTemplate.Create(template, requiredParams, description));
        }
        
        role.ReplacePermissions(templates);
    }

    public async Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Roles";
        
        var roles = new List<Role>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ids = new List<Guid>();
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(Guid.Parse(reader.GetString(0)));
        }
        reader.Close();

        foreach (var id in ids)
        {
            var role = await GetRoleAsync(connection, "Id", id.ToString(), cancellationToken);
            if (role != null) roles.Add(role);
        }
        return roles;
    }

    public async Task SaveAsync(Role role, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO Roles (Id, Code, Name, NormalizedName, Description, IsSystemRole, ConcurrencyStamp)
                    VALUES (@Id, @Code, @Name, @NormalizedName, @Description, @IsSystemRole, @ConcurrencyStamp)
                    ON CONFLICT(Id) DO UPDATE SET
                        Code = @Code,
                        Name = @Name,
                        NormalizedName = @NormalizedName,
                        Description = @Description,
                        IsSystemRole = @IsSystemRole,
                        ConcurrencyStamp = @ConcurrencyStamp";
                
                command.Parameters.AddWithValue("@Id", role.Id.ToString());
                command.Parameters.AddWithValue("@Code", role.Code);
                command.Parameters.AddWithValue("@Name", role.Name);
                command.Parameters.AddWithValue("@NormalizedName", role.NormalizedName);
                command.Parameters.AddWithValue("@Description", (object?)role.Description ?? DBNull.Value);
                command.Parameters.AddWithValue("@IsSystemRole", role.IsSystemRole ? 1 : 0);
                command.Parameters.AddWithValue("@ConcurrencyStamp", role.ConcurrencyStamp ?? Guid.NewGuid().ToString());
                
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM RolePermissions WHERE RoleId = @RoleId";
                command.Parameters.AddWithValue("@RoleId", role.Id.ToString());
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var perm in role.PermissionGrants)
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"
                        INSERT INTO RolePermissions (RoleId, IdentifierTemplate, Description, RequiredParameters)
                        VALUES (@RoleId, @IdentifierTemplate, @Description, @RequiredParameters)";
                    
                    command.Parameters.AddWithValue("@RoleId", role.Id.ToString());
                    command.Parameters.AddWithValue("@IdentifierTemplate", perm.IdentifierTemplate);
                    command.Parameters.AddWithValue("@Description", (object?)perm.Description ?? DBNull.Value);
                    command.Parameters.AddWithValue("@RequiredParameters", JsonSerializer.Serialize(perm.RequiredParameters));
                    
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Roles WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id.ToString());
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }
}
