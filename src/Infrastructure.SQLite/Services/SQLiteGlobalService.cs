using AbsolutePathHelpers;
using Application.Configuration.Extensions;
using Microsoft.Extensions.Configuration;
using SQLite;
using System.Diagnostics.CodeAnalysis;

namespace Infrastructure.SQLite.Services;

public class SQLiteGlobalService(IConfiguration configuration)
{
    private readonly SemaphoreSlim locker = new(1);

    private SQLiteAsyncConnection? db = null;

    public async Task<SQLiteAsyncConnection> Bootstrap()
    {
        if (db == null)
        {
            try
            {
                await locker.WaitAsync();
                if (db == null)
                {
                    var dbPath = configuration.GetHomePath() / "local.db";
                    dbPath.Parent?.CreateDirectory();
                    db = new(dbPath);
                }
            }
            finally
            {
                locker.Release();
            }
        }
        return db;
    }

    public async Task<SQLiteAsyncConnection> BootstrapTable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
         where T : new()
    {
        var db = await Bootstrap();
        await db.CreateTableAsync<T>();
        return db;
    }
}
