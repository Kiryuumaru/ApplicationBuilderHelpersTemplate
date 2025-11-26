namespace Infrastructure.Sqlite;

public interface IDatabaseBootstrap
{
    Task SetupAsync(CancellationToken cancellationToken = default);
}
