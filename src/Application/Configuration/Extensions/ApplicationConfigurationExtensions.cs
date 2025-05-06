using Application.Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace Application.Configuration.Extensions;

public static class ApplicationConfigurationExtensions
{
    private const string AppTagOverrideKey = "APP_TAG_OVERRIDE";
    public static string GetAppTagOverride(this IConfiguration configuration)
    {
        return configuration.GetVarRefValue(AppTagOverrideKey);
    }
    public static void SetAppTagOverride(this IConfiguration configuration, string appTag)
    {
        configuration[AppTagOverrideKey] = appTag;
    }
}
