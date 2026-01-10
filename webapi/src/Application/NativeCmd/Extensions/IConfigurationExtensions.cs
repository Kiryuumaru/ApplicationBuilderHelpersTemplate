using Application.Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace Application.NativeCmd.Extensions;

public static class IConfigurationExtensions
{
    private const string IsVerboseCliLoggerKey = "RUNTIME_ISVERBOSECLILOGGER";

    public static bool GetIsVerboseCliLogger(this IConfiguration configuration)
    {
        return configuration.GetBooleanOrDefault(IsVerboseCliLoggerKey, true);
    }

    public static void SetIsVerboseCliLogger(this IConfiguration configuration, bool isVerboseCliLogger)
    {
        configuration.SetBoolean(IsVerboseCliLoggerKey, isVerboseCliLogger);
    }
}
