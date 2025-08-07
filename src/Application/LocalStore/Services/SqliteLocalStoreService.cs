using Application.LocalStore.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Application.LocalStore.Extensions;
using System.Data;
using DisposableHelpers.Attributes;
using AbsolutePathHelpers;

namespace Application.LocalStore.Services;

[Disposable]
internal partial class SqliteLocalStoreService : ILocalStoreService
{
    private readonly AbsolutePath dbPath;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationLock = new(1);
    private static bool _isInitialized = false;
    
    // Transaction-scoped state
    private SqliteConnection? _connection;
    private SqliteTransaction? _transaction;

    public SqliteLocalStoreService(IConfiguration configuration)
    {
        dbPath = configuration.GetLocalStoreDbPath();
        
        // Configure SQLite for optimal concurrency
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath.ToString(),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            // Enable WAL mode for better concurrency
            DefaultTimeout = 30
        };
        
        _connectionString = connectionStringBuilder.ToString();
    }

    public async Task Open(CancellationToken cancellationToken)
    {
        if (_connection != null)
        {
            throw new InvalidOperationException("Service is already open. Call Close or dispose before opening again.");
        }

        await EnsureInitializedAsync(cancellationToken);
        
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync(cancellationToken);
        _transaction = await _connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) as SqliteTransaction;
        
        if (_transaction == null)
        {
            throw new InvalidOperationException("Failed to create SQLite transaction");
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized) return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;

            dbPath.Parent?.CreateDirectory();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Enable WAL mode for better concurrency
            using var walCommand = connection.CreateCommand();
            walCommand.CommandText = "PRAGMA journal_mode=WAL;";
            await walCommand.ExecuteNonQueryAsync(cancellationToken);

            // Enable foreign keys and optimize for performance
            using var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = @"
                PRAGMA foreign_keys=ON;
                PRAGMA synchronous=NORMAL;
                PRAGMA cache_size=10000;
                PRAGMA temp_store=memory;
                PRAGMA busy_timeout=30000;";
            await pragmaCommand.ExecuteNonQueryAsync(cancellationToken);

            // Create the table with proper indexes
            using var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS LocalStoreData (
                    Id TEXT PRIMARY KEY,
                    Group TEXT NOT NULL,
                    Data TEXT,
                    RawId TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );
                
                CREATE INDEX IF NOT EXISTS IX_LocalStoreData_Group ON LocalStoreData(Group);
                CREATE INDEX IF NOT EXISTS IX_LocalStoreData_RawId ON LocalStoreData(RawId);
                CREATE INDEX IF NOT EXISTS IX_LocalStoreData_Group_RawId ON LocalStoreData(Group, RawId);
                
                -- Trigger to update UpdatedAt timestamp
                CREATE TRIGGER IF NOT EXISTS update_LocalStoreData_timestamp 
                AFTER UPDATE ON LocalStoreData
                BEGIN
                    UPDATE LocalStoreData SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;";

            await createTableCommand.ExecuteNonQueryAsync(cancellationToken);
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<string> Get(string group, string id, CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        
        string rawId = $"{id}__{group}";
        
        using var command = _connection!.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = "SELECT Data FROM LocalStoreData WHERE RawId = @rawId AND Group = @group";
        command.Parameters.AddWithValue("@rawId", rawId);
        command.Parameters.AddWithValue("@group", group);
        
        var data = await command.ExecuteScalarAsync(cancellationToken) as string;
        return data ?? string.Empty;
    }

    public async Task<string[]> GetIds(string group, CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        
        using var command = _connection!.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = "SELECT RawId FROM LocalStoreData WHERE Group = @group ORDER BY Id";
        command.Parameters.AddWithValue("@group", group);
        
        var ids = new List<string>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        while (await reader.ReadAsync(cancellationToken))
        {
            var rawId = reader.GetString(0);
            if (!string.IsNullOrEmpty(rawId))
            {
                // Extract the original id from the rawId format (id__group)
                var idPostfix = $"__{group}";
                if (rawId.EndsWith(idPostfix))
                {
                    var originalId = rawId[..^idPostfix.Length];
                    ids.Add(originalId);
                }
            }
        }
        
        return [.. ids];
    }

    public async Task Set(string group, string id, string? data, CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        
        string rawId = $"{id}__{group}";
        
        if (data == null)
        {
            // Delete the record
            using var deleteCommand = _connection!.CreateCommand();
            deleteCommand.Transaction = _transaction;
            deleteCommand.CommandText = "DELETE FROM LocalStoreData WHERE RawId = @rawId AND Group = @group";
            deleteCommand.Parameters.AddWithValue("@rawId", rawId);
            deleteCommand.Parameters.AddWithValue("@group", group);
            
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            // Insert or update the record
            using var upsertCommand = _connection!.CreateCommand();
            upsertCommand.Transaction = _transaction;
            upsertCommand.CommandText = @"
                INSERT INTO LocalStoreData (Id, Group, Data, RawId) 
                VALUES (@id, @group, @data, @rawId)
                ON CONFLICT(Id) DO UPDATE SET 
                    Data = @data,
                    Group = @group,
                    RawId = @rawId,
                    UpdatedAt = CURRENT_TIMESTAMP";
                    
            upsertCommand.Parameters.AddWithValue("@id", rawId);
            upsertCommand.Parameters.AddWithValue("@group", group);
            upsertCommand.Parameters.AddWithValue("@data", data);
            upsertCommand.Parameters.AddWithValue("@rawId", rawId);
            
            await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<bool> Contains(string group, string id, CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        
        string rawId = $"{id}__{group}";
        
        using var command = _connection!.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = "SELECT COUNT(*) FROM LocalStoreData WHERE RawId = @rawId AND Group = @group";
        command.Parameters.AddWithValue("@rawId", rawId);
        command.Parameters.AddWithValue("@group", group);
        
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
    }

    private void ThrowIfNotOpen()
    {
        if (_connection == null || _transaction == null)
        {
            throw new InvalidOperationException("Service is not open. Call Open() before using the service.");
        }

        VerifyNotDisposed();
    }

    protected void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                // Commit the transaction if it hasn't been committed or rolled back
                if (_transaction?.Connection != null)
                {
                    _transaction.Commit();
                }
            }
            catch
            {
                // If commit fails, try to rollback
                try
                {
                    if (_transaction?.Connection != null)
                    {
                        _transaction.Rollback();
                    }
                }
                catch
                {
                    // Ignore rollback failures during disposal
                }
            }
            finally
            {
                _transaction?.Dispose();
                _connection?.Dispose();
            }
        }
    }
}
