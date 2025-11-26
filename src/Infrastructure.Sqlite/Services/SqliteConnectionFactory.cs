using System.Data;
using Microsoft.Data.Sqlite;

namespace Infrastructure.Sqlite.Services;

public sealed class SqliteConnectionFactory : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _keepAliveConnection;
    private readonly object _lock = new();

    public SqliteConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }
        _connectionString = connectionString;
        
        // For in-memory databases with shared cache, keep one connection open
        // to prevent the database from being destroyed when all connections close
        if (connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
        {
            _keepAliveConnection = new SqliteConnection(_connectionString);
            _keepAliveConnection.Open();
        }
    }

    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
    
    public async Task<SqliteConnection> CreateOpenedConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_keepAliveConnection != null)
            {
                _keepAliveConnection.Dispose();
                _keepAliveConnection = null;
            }
        }
    }
}
