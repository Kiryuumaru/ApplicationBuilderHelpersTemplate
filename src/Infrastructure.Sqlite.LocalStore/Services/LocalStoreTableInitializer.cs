using Infrastructure.Sqlite.Services;

namespace Infrastructure.Sqlite.LocalStore.Services;

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
