using ApplicationBuilderHelpers.Extensions;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.EFCore.Sqlite.Extensions;

public static class ConfigurationExtensions
{
    private const string SqliteConnectionStringKey = "SQLITE_CONNECTION_STRING";
    public static string GetSqliteConnectionString(this IConfiguration configuration)
    {
        return configuration.GetRefValueOrDefault(SqliteConnectionStringKey, "Data Source=app.db");
    }
    public static void SetSqliteConnectionString(this IConfiguration configuration, string connectionString)
    {
        configuration[SqliteConnectionStringKey] = connectionString;
    }
}
