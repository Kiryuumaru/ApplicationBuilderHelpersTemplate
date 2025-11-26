namespace Infrastructure.Sqlite;

public abstract class DatabaseBootstrap(SqliteConnectionFactory connectionFactory) : IDatabaseBootstrap
{
    protected readonly SqliteConnectionFactory ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public abstract Task SetupAsync(CancellationToken cancellationToken = default);

    protected async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken = default)
    {
        using var connection = await ConnectionFactory.CreateOpenedConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
