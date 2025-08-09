using AbsolutePathHelpers;
using ApplicationBuilderHelpers.Extensions;
using Microsoft.Extensions.Configuration;

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