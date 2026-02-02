using ApplicationBuilderHelpers.Extensions;
using Microsoft.Extensions.Configuration;

namespace Application.AppEnvironment.Extensions;

public static class AppEnvironmentConfigurationExtensions
{
    private const string AppTagOverrideKey = "RUNTIME_APP_TAG_OVERRIDE";

    public static string? GetAppTagOverride(this IConfiguration configuration)
    {
        return configuration.GetRefValueOrDefault(AppTagOverrideKey);
    }

    public static void SetAppTagOverride(this IConfiguration configuration, string appTag)
    {
        configuration[AppTagOverrideKey] = appTag;
    }
}
