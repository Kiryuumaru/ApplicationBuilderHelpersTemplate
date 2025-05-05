using AbsolutePathHelpers;
using Application.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Configuration.Extensions;

public static class CommonConfigurationExtensions
{
    private static Guid? _runtimeGuid = null;
    public static Guid GetRuntimeGuid(this IConfiguration configuration)
    {
        if (_runtimeGuid == null)
        {
            _runtimeGuid = Guid.Parse(configuration.GetVarRefValueOrDefault("VEG_RUNTIME_RUNTIME_GUID", Guid.NewGuid().ToString()));
        }
        return _runtimeGuid.Value;
    }

    private const string ServiceNameKey = "VEG_RUNTIME_SERVICE_NAME";
    public static string GetServiceName(this IConfiguration configuration)
    {
        return configuration.GetVarRefValue(ServiceNameKey);
    }
    public static void SetServiceName(this IConfiguration configuration, string serviceName)
    {
        configuration[ServiceNameKey] = serviceName;
    }

    private const string LogsDumpDirectoryKey = "VEG_RUNTIME_LOGS_DUMP_DIRECTORY";
    public static AbsolutePath? GetLogsDumpDirectory(this IConfiguration configuration)
    {
        var logsDumpDirStr = configuration.GetVarRefValueOrDefault(LogsDumpDirectoryKey);
        if (string.IsNullOrWhiteSpace(logsDumpDirStr))
        {
            return null;
        }
        return AbsolutePath.Create(logsDumpDirStr);
    }
    public static void SetLogsDumpDirectory(this IConfiguration configuration, AbsolutePath? logsDumpDir)
    {
        configuration[LogsDumpDirectoryKey] = logsDumpDir?.ToString();
    }

    private const string LoggerLevelKey = "VEG_RUNTIME_LOGGER_LEVEL";
    public static LogLevel GetLoggerLevel(this IConfiguration configuration)
    {
        var loggerLevel = configuration.GetVarRefValueOrDefault(LoggerLevelKey, LogLevel.Information.ToString());
        return Enum.Parse<LogLevel>(loggerLevel);
    }
    public static void SetLoggerLevel(this IConfiguration configuration, LogLevel loggerLevel)
    {
        configuration[LoggerLevelKey] = loggerLevel.ToString();
    }

    private const string HomePathKey = "VEG_RUNTIME_HOME_PATH";
    public static AbsolutePath GetHomePath(this IConfiguration configuration)
    {
        return configuration.GetVarRefValueOrDefault(HomePathKey, AbsolutePath.Create(Environment.CurrentDirectory));
    }
    public static void SetHomePath(this IConfiguration configuration, AbsolutePath dataPath)
    {
        configuration[HomePathKey] = dataPath;
    }

    public static AbsolutePath GetDataPath(this IConfiguration configuration)
    {
        return GetHomePath(configuration) / ".data";
    }

    public static AbsolutePath GetTempPath(this IConfiguration configuration)
    {
        return GetHomePath(configuration) / "temp";
    }

    public static AbsolutePath GetDownloadsPath(this IConfiguration configuration)
    {
        return GetHomePath(configuration) / "downloads";
    }

    public static AbsolutePath GetServicesPath(this IConfiguration configuration)
    {
        return GetHomePath(configuration) / "svc";
    }

    public static AbsolutePath GetBinPath(this IConfiguration configuration)
    {
        return GetHomePath(configuration) / "bin";
    }

    public static AbsolutePath GetAssetsPath(this IConfiguration configuration)
    {
        return GetHomePath(configuration) / "assets";
    }

    public static AbsolutePath GetReleasesPath(this IConfiguration configuration)
    {
        return GetHomePath(configuration) / "releases";
    }

    public static AbsolutePath GetDaemonsPath(this IConfiguration configuration)
    {
        return GetHomePath(configuration) / "daemon";
    }
}
