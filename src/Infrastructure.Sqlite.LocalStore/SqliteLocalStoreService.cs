using System.Data;
using Application.LocalStore.Interfaces;
using Microsoft.Data.Sqlite;

namespace Infrastructure.Sqlite.LocalStore;

public sealed class SqliteLocalStoreService(SqliteConnectionFactory connectionFactory) : ILocalStoreService
{
    private readonly SqliteConnectionFactory _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    private SqliteConnection? _connection;
    private SqliteTransaction? _transaction;

    public async Task Open(CancellationToken cancellationToken)
    {
        if (_connection != null)
        {
            return;
        }

        _connection = (SqliteConnection)await _connectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        _transaction = _connection.BeginTransaction();
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        
        // Start a new transaction for subsequent operations if needed, or just leave it.
        // Usually Commit ends the unit of work.
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection == null)
        {
            await Open(cancellationToken);
        }
    }

    public async Task<string?> Get(string group, string id, CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(cancellationToken);
        
        using var command = _connection!.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = "SELECT Data FROM LocalStore WHERE \"Group\" = @Group AND \"Id\" = @Id";
        command.Parameters.AddWithValue("@Group", group);
        command.Parameters.AddWithValue("@Id", id);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    public async Task<string[]> GetIds(string group, CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(cancellationToken);

        using var command = _connection!.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = "SELECT \"Id\" FROM LocalStore WHERE \"Group\" = @Group";
        command.Parameters.AddWithValue("@Group", group);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ids = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetString(0));
        }
        return ids.ToArray();
    }

    public async Task Set(string group, string id, string? data, CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(cancellationToken);

        using var command = _connection!.CreateCommand();
        command.Transaction = _transaction;
        if (data == null)
        {
            command.CommandText = "DELETE FROM LocalStore WHERE \"Group\" = @Group AND \"Id\" = @Id";
        }
        else
        {
            command.CommandText = @"
                INSERT INTO LocalStore (""Group"", ""Id"", ""Data"") 
                VALUES (@Group, @Id, @Data)
                ON CONFLICT(""Group"", ""Id"") DO UPDATE SET ""Data"" = @Data";
            command.Parameters.AddWithValue("@Data", data);
        }
        command.Parameters.AddWithValue("@Group", group);
        command.Parameters.AddWithValue("@Id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> Contains(string group, string id, CancellationToken cancellationToken)
    {
        await EnsureConnectionAsync(cancellationToken);

        using var command = _connection!.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = "SELECT 1 FROM LocalStore WHERE \"Group\" = @Group AND \"Id\" = @Id";
        command.Parameters.AddWithValue("@Group", group);
        command.Parameters.AddWithValue("@Id", id);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
    }
}
