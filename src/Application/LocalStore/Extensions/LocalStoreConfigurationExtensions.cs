using AbsolutePathHelpers;
using Application.Common.Extensions;
using ApplicationBuilderHelpers.Extensions;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.LocalStore.Extensions;

public static class LocalStoreConfigurationExtensions
{
    private const string LocalStoreDbPathKey = "RUNTIME_LOCAL_STORE_DB_PATH";
    public static AbsolutePath GetLocalStoreDbPath(this IConfiguration configuration)
    {
        return configuration.GetRefValueOrDefault(LocalStoreDbPathKey, 
            AbsolutePath.Create(Environment.CurrentDirectory) / "localstore.db");
    }
    public static void SetLocalStoreDbPath(this IConfiguration configuration, AbsolutePath path)
    {
        configuration[LocalStoreDbPathKey] = path;
    }
}
