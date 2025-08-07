using Application.LocalStore.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Application.LocalStore.Extensions;

namespace Application.LocalStore.Services;

internal class SqliteLocalStoreService : ILocalStoreService
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized = false;

    public SqliteLocalStoreService(IConfiguration configuration)
    {
        var dbPath = configuration.GetLocalStoreDbPath();
        var parentPath = dbPath.Parent;
        if (parentPath != null && !Directory.Exists(parentPath.ToString()))
        {
            Directory.CreateDirectory(parentPath.ToString());
        }
        
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

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized) return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;

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
                PRAGMA temp_store=memory;";
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
        await EnsureInitializedAsync(cancellationToken);
        
        string rawId = $"{id}__{group}";
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Data FROM LocalStoreData WHERE RawId = @rawId AND Group = @group";
        command.Parameters.AddWithValue("@rawId", rawId);
        command.Parameters.AddWithValue("@group", group);
        
        var data = await command.ExecuteScalarAsync(cancellationToken) as string;
        return data ?? string.Empty;
    }

    public async Task<string[]> GetIds(string group, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        using var command = connection.CreateCommand();
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
        
        return ids.ToArray();
    }

    public async Task Set(string group, string id, string? data, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        string rawId = $"{id}__{group}";
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        // Use a transaction for consistency
        using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        
        if (data == null)
        {
            // Delete the record
            using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM LocalStoreData WHERE RawId = @rawId AND Group = @group";
            deleteCommand.Parameters.AddWithValue("@rawId", rawId);
            deleteCommand.Parameters.AddWithValue("@group", group);
            
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            // Insert or update the record
            using var upsertCommand = connection.CreateCommand();
            upsertCommand.Transaction = transaction;
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
        
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> Contains(string group, string id, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        string rawId = $"{id}__{group}";
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM LocalStoreData WHERE RawId = @rawId AND Group = @group";
        command.Parameters.AddWithValue("@rawId", rawId);
        command.Parameters.AddWithValue("@group", group);
        
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }
}
