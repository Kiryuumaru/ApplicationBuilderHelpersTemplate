using Application.Common.Extensions;
using ApplicationBuilderHelpers.Extensions;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

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
