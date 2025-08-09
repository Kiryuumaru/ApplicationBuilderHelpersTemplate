using Application.Abstractions.LocalStore;
using Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sqlite.LocalStore.Extensions;

internal static class SqliteLocalStoreServiceCollectionExtensions
{
    public static IServiceCollection AddSqliteLocalStoreServices(this IServiceCollection services)
    {
        services.AddScoped<ILocalStoreService, SqliteLocalStoreService>();
        return services;
    }
}
