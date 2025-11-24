using AbsolutePathHelpers;
using Application.LocalStore.Common;
using Application.LocalStore.Extensions;
using Application.LocalStore.Interfaces;
using DisposableHelpers.Attributes;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Application.LocalStore.Services;

[Disposable]
internal partial class SqliteLocalStoreService : ILocalStoreService
{
    private readonly AbsolutePath dbPath;
    private readonly string connectionString;
    private readonly TimeSpan commandTimeout = TimeSpan.FromSeconds(DefaultCommandTimeoutSeconds);

    private const int DefaultCommandTimeoutSeconds = 30;
    private const int MaxRetryCount = 5;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly SemaphoreSlim GlobalInitializationLock = new(1, 1);
    private static bool isGloballyInitialized;

    private SqliteConnection? connection;
    private SqliteTransaction? transaction;

    public SqliteLocalStoreService(IConfiguration configuration)
    {
        dbPath = configuration.GetLocalStoreDbPath();
        connectionString = BuildConnectionString(dbPath);
    }

    private static string BuildConnectionString(AbsolutePath path)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = path.ToString(),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = DefaultCommandTimeoutSeconds
        };

        return connectionStringBuilder.ToString();
    }

    public async Task Open(CancellationToken cancellationToken)
    {
        if (connection != null)
        {
            throw new InvalidOperationException("Service is already open. Call Close or dispose before opening again.");
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) as SqliteTransaction;

        if (transaction is null)
        {
            throw new InvalidOperationException("Failed to create SQLite transaction");
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (isGloballyInitialized)
        {
            return;
        }

        await GlobalInitializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (isGloballyInitialized)
            {
                return;
            }

            dbPath.Parent?.CreateDirectory();

            using var initConnection = new SqliteConnection(connectionString);
            await initConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await SetWalModeAsync(initConnection, cancellationToken).ConfigureAwait(false);
                await ExecutePragmaAsync(initConnection, "PRAGMA foreign_keys=ON;", cancellationToken).ConfigureAwait(false);
                await ExecutePragmaAsync(initConnection, "PRAGMA synchronous=NORMAL;", cancellationToken).ConfigureAwait(false);
                await ExecutePragmaAsync(initConnection, "PRAGMA cache_size=10000;", cancellationToken).ConfigureAwait(false);
                await ExecutePragmaAsync(initConnection, "PRAGMA temp_store=memory;", cancellationToken).ConfigureAwait(false);
                await ExecutePragmaAsync(initConnection, "PRAGMA busy_timeout=30000;", cancellationToken).ConfigureAwait(false);
                await ExecutePragmaAsync(initConnection, "PRAGMA mmap_size=268435456;", cancellationToken).ConfigureAwait(false);

                await EnsureSchemaAsync(initConnection, cancellationToken).ConfigureAwait(false);

                isGloballyInitialized = true;
            }
            catch (SqliteException ex)
            {
                throw new InvalidOperationException($"Failed to initialize SQLite database: {ex.Message} (Error Code: {ex.SqliteErrorCode})", ex);
            }
        }
        finally
        {
            GlobalInitializationLock.Release();
        }
    }

    private static async Task SetWalModeAsync(SqliteConnection initConnection, CancellationToken cancellationToken)
    {
        using var walCommand = initConnection.CreateCommand();
        walCommand.CommandTimeout = DefaultCommandTimeoutSeconds;
        walCommand.CommandText = "PRAGMA journal_mode=WAL;";
        await walCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSchemaAsync(SqliteConnection initConnection, CancellationToken cancellationToken)
    {
        await ExecuteCommandAsync(initConnection,
            """
            CREATE TABLE IF NOT EXISTS LocalStoreData (
                Id TEXT PRIMARY KEY,
                [Group] TEXT NOT NULL,
                Data TEXT,
                RawId TEXT NOT NULL,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            """,
            cancellationToken).ConfigureAwait(false);

        await ExecuteCommandAsync(initConnection,
            "CREATE INDEX IF NOT EXISTS IX_LocalStoreData_Group ON LocalStoreData([Group]);",
            cancellationToken).ConfigureAwait(false);

        await ExecuteCommandAsync(initConnection,
            "CREATE INDEX IF NOT EXISTS IX_LocalStoreData_RawId ON LocalStoreData(RawId);",
            cancellationToken).ConfigureAwait(false);

        await ExecuteCommandAsync(initConnection,
            "CREATE INDEX IF NOT EXISTS IX_LocalStoreData_Group_RawId ON LocalStoreData([Group], RawId);",
            cancellationToken).ConfigureAwait(false);

        await ExecuteCommandAsync(initConnection,
            """
            CREATE TRIGGER IF NOT EXISTS update_LocalStoreData_timestamp
            AFTER UPDATE ON LocalStoreData
            FOR EACH ROW
            BEGIN
                UPDATE LocalStoreData SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
            END;
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecutePragmaAsync(SqliteConnection connection, string pragmaCommand, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = DefaultCommandTimeoutSeconds;
        command.CommandText = pragmaCommand;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteCommandAsync(SqliteConnection connection, string commandText, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = DefaultCommandTimeoutSeconds;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private SqliteCommand CreateCommand(string commandText)
    {
        var command = connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = (int)commandTimeout.TotalSeconds;
        command.CommandText = commandText;
        return command;
    }

    private static (string Group, string Id, string StorageKey) ComposeKeys(string group, string id)
    {
        LocalStoreKey.NormalizePair(ref group, ref id);
        return (group, id, LocalStoreKey.BuildStorageKey(group, id));
    }

    public Task<string?> Get(string group, string id, CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        var (normalizedGroup, _, storageKey) = ComposeKeys(group, id);

        return ExecuteWithRetryAsync(async () =>
        {
            using var command = CreateCommand("SELECT Data FROM LocalStoreData WHERE RawId = @rawId AND [Group] = @group");
            command.Parameters.AddWithValue("@rawId", storageKey);
            command.Parameters.AddWithValue("@group", normalizedGroup);

            var data = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return data as string;
        }, cancellationToken);
    }

    public Task<string[]> GetIds(string group, CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        var normalizedGroup = LocalStoreKey.NormalizeGroup(group);

        return ExecuteWithRetryAsync(async () =>
        {
            using var command = CreateCommand("SELECT RawId FROM LocalStoreData WHERE [Group] = @group ORDER BY Id");
            command.Parameters.AddWithValue("@group", normalizedGroup);

            var ids = new List<string>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var rawId = reader.GetString(0);
                if (string.IsNullOrEmpty(rawId))
                {
                    continue;
                }

                if (LocalStoreKey.TryExtractIdFromStorageKey(rawId, normalizedGroup, out var originalId))
                {
                    ids.Add(originalId);
                }
            }

            return ids.ToArray();
        }, cancellationToken);
    }

    public Task Set(string group, string id, string? data, CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        var (normalizedGroup, _, storageKey) = ComposeKeys(group, id);

        return ExecuteWithRetryAsync(async () =>
        {
            if (data is null)
            {
                using var deleteCommand = CreateCommand("DELETE FROM LocalStoreData WHERE RawId = @rawId AND [Group] = @group");
                deleteCommand.Parameters.AddWithValue("@rawId", storageKey);
                deleteCommand.Parameters.AddWithValue("@group", normalizedGroup);
                await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            using var upsertCommand = CreateCommand(
                """
                INSERT INTO LocalStoreData (Id, [Group], Data, RawId)
                VALUES (@id, @group, @data, @rawId)
                ON CONFLICT(Id) DO UPDATE SET
                    Data = @data,
                    [Group] = @group,
                    RawId = @rawId,
                    UpdatedAt = CURRENT_TIMESTAMP
                """
            );
            upsertCommand.Parameters.AddWithValue("@id", storageKey);
            upsertCommand.Parameters.AddWithValue("@group", normalizedGroup);
            upsertCommand.Parameters.AddWithValue("@data", data);
            upsertCommand.Parameters.AddWithValue("@rawId", storageKey);

            await upsertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<bool> Contains(string group, string id, CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        var (normalizedGroup, _, storageKey) = ComposeKeys(group, id);

        return ExecuteWithRetryAsync(async () =>
        {
            using var command = CreateCommand("SELECT COUNT(*) FROM LocalStoreData WHERE RawId = @rawId AND [Group] = @group");
            command.Parameters.AddWithValue("@rawId", storageKey);
            command.Parameters.AddWithValue("@group", normalizedGroup);

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var count = Convert.ToInt32(result);
            return count > 0;
        }, cancellationToken);
    }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        if (transaction is null)
        {
            return Task.CompletedTask;
        }

        return ExecuteWithRetryAsync(async () =>
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotOpen();
        if (transaction is null)
        {
            return Task.CompletedTask;
        }

        return ExecuteWithRetryAsync(async () =>
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    private Task ExecuteWithRetryAsync(Func<Task> operation, CancellationToken cancellationToken)
        => ExecuteWithRetryAsync(async () =>
        {
            await operation().ConfigureAwait(false);
            return true;
        }, cancellationToken);

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var delay = InitialRetryDelay;
        SqliteException? lastException = null;

        for (var attempt = 1; attempt <= MaxRetryCount; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (SqliteException ex) when (IsTransient(ex) && attempt < MaxRetryCount)
            {
                lastException = ex;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 500));
            }
        }

        throw new InvalidOperationException("Failed to execute SQLite command after multiple retries.", lastException);
    }

    private static bool IsTransient(SqliteException exception)
        => exception.SqliteErrorCode is 5 or 6;

    private void ThrowIfNotOpen()
    {
        if (connection == null || transaction == null)
        {
            throw new InvalidOperationException("Service is not open. Call Open() before using the service.");
        }

        VerifyNotDisposed();
    }

    protected void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        try
        {
            if (transaction?.Connection != null)
            {
                transaction.Commit();
            }
        }
        catch
        {
            try
            {
                if (transaction?.Connection != null)
                {
                    transaction.Rollback();
                }
            }
            catch
            {
            }
        }
        finally
        {
            transaction?.Dispose();
            connection?.Dispose();
            transaction = null;
            connection = null;
        }
    }
}
