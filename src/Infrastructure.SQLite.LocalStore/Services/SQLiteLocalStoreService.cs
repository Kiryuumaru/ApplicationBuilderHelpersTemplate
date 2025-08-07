using Application.LocalStore.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.SQLite.LocalStore.Services;

public class SQLiteLocalStoreService(IServiceProvider serviceProvider) : ILocalStoreService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task<string> Get(string group, string id, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sqliteLocalDb = scope.ServiceProvider.GetRequiredService<SQLiteLocalStoreGlobalService>();

        var value = await Task.Run(async () => await sqliteLocalDb.Get(id, group), cancellationToken);
        return value ?? string.Empty;
    }

    public async Task<string[]> GetIds(string group, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sqliteLocalDb = scope.ServiceProvider.GetRequiredService<SQLiteLocalStoreGlobalService>();

        var value = await Task.Run(async () => await sqliteLocalDb.GetIds(group), cancellationToken);
        return value;
    }

    public async Task Set(string group, string id, string? data, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sqliteLocalDb = scope.ServiceProvider.GetRequiredService<SQLiteLocalStoreGlobalService>();

        await Task.Run(async () => await sqliteLocalDb.Set(id, group, data), cancellationToken);
    }

    public async Task<bool> Contains(string group, string id, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sqliteLocalDb = scope.ServiceProvider.GetRequiredService<SQLiteLocalStoreGlobalService>();

        try
        {
            var value = await Task.Run(async () => await sqliteLocalDb.Get(id, group), cancellationToken);
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }
}
