using Infrastructure.EFCore.Sqlite.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.EFCore.Sqlite.Services;

/// <summary>
/// Holds an open SQLite connection for in-memory databases with shared cache.
/// This prevents the database from being destroyed when all other connections close.
/// </summary>
public sealed class SqliteConnectionHolder(IConfiguration configuration) : IDisposable
{
    private SqliteConnection? _keepAliveConnection;

    public void EnsureOpen()
    {
        // For in-memory databases with shared cache, keep one connection open
        // to prevent the database from being destroyed when all connections close
        var connectionString = configuration.GetSqliteConnectionString();
        if (_keepAliveConnection == null && connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
        {
            _keepAliveConnection = new SqliteConnection(connectionString);
            _keepAliveConnection.Open();
        }
    }

    public void Dispose()
    {
        if (_keepAliveConnection != null)
        {
            _keepAliveConnection.Dispose();
            _keepAliveConnection = null;
        }
    }
}
