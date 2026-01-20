using Infrastructure.EFCore.Sqlite.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.EFCore.Sqlite.Services;

/// <summary>
/// Holds an open SQLite connection for in-memory databases with shared cache.
/// This prevents the database from being destroyed when all other connections close.
/// </summary>
public sealed class SqliteConnectionHolder : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _keepAliveConnection;

    public SqliteConnectionHolder(IConfiguration configuration)
    {
        _connectionString = configuration.GetSqliteConnectionString();
    }

    public SqliteConnectionHolder(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Opens the keep-alive connection if using in-memory mode.
    /// Must be called before any database operations.
    /// </summary>
    public void EnsureOpen()
    {
        if (_keepAliveConnection != null)
        {
            return;
        }

        // For in-memory databases with shared cache, keep one connection open
        // to prevent the database from being destroyed when all connections close
        if (_connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
        {
            _keepAliveConnection = new SqliteConnection(_connectionString);
            _keepAliveConnection.Open();
        }
    }

    public void Dispose()
    {
        _keepAliveConnection?.Dispose();
        _keepAliveConnection = null;
    }
}
