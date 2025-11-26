using Infrastructure.Sqlite;

namespace Infrastructure.Sqlite.LocalStore;

public sealed class LocalStoreTableInitializer(SqliteConnectionFactory connectionFactory) : DatabaseBootstrap(connectionFactory)
{
    public override async Task SetupAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS LocalStore (
                ""Group"" TEXT NOT NULL,
                ""Id"" TEXT NOT NULL,
                ""Data"" TEXT,
                PRIMARY KEY (""Group"", ""Id"")
            );";

        await ExecuteSqlAsync(sql, cancellationToken);
    }
}
