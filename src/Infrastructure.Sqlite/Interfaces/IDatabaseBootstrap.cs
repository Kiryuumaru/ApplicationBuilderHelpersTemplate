namespace Infrastructure.Sqlite.Interfaces;

public interface IDatabaseBootstrap
{
    Task SetupAsync(CancellationToken cancellationToken = default);
}
