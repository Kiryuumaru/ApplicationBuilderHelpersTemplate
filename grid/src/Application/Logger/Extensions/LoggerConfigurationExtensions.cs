using AbsolutePathHelpers;
using ApplicationBuilderHelpers.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Logger.Extensions;

/// <summary>
/// Configuration extensions for logger settings.
/// Allows BaseCommand to store settings that Infrastructure can read.
/// Supports @ref: reference chains via GetRefValueOrDefault.
/// </summary>
public static class LoggerConfigurationExtensions
{
    private static Guid? _runtimeGuid = null;
    private const string LoggerLevelKey = "RUNTIME_LOGGER_LEVEL";
    private const string ApplyThemeWhenOutputIsRedirectedKey = "RUNTIME_APPLY_THEME_WHEN_OUTPUT_IS_REDIRECTED";
    private const string LogsDumpDirectoryKey = "RUNTIME_LOGS_DUMP_DIRECTORY";
    private const string LogsPathKey = "RUNTIME_LOGS_PATH";
    private const string RuntimeGuidKey = "RUNTIME_GUID";

    public static Guid GetRuntimeGuid(this IConfiguration configuration)
    {
        if (_runtimeGuid == null)
        {
            _runtimeGuid = Guid.Parse(configuration.GetRefValueOrDefault(RuntimeGuidKey, Guid.NewGuid().ToString()));
        }
        return _runtimeGuid.Value;
    }

    public static void SetRuntimeGuid(this IConfiguration configuration, Guid runtimeGuid)
    {
        configuration[RuntimeGuidKey] = runtimeGuid.ToString();
        _runtimeGuid = runtimeGuid;
    }

    public static LogLevel GetLoggerLevel(this IConfiguration configuration)
    {
        var loggerLevel = configuration.GetRefValueOrDefault(LoggerLevelKey, LogLevel.Information.ToString());
        return Enum.Parse<LogLevel>(loggerLevel);
    }

    public static void SetLoggerLevel(this IConfiguration configuration, LogLevel loggerLevel)
    {
        configuration[LoggerLevelKey] = loggerLevel.ToString();
    }

    public static bool GetApplyThemeWhenOutputIsRedirected(this IConfiguration configuration)
    {
        var value = configuration.GetRefValueOrDefault(ApplyThemeWhenOutputIsRedirectedKey, "false");
        return bool.TryParse(value, out var result) && result;
    }

    public static void SetApplyThemeWhenOutputIsRedirected(this IConfiguration configuration, bool value)
    {
        configuration[ApplyThemeWhenOutputIsRedirectedKey] = value.ToString().ToLowerInvariant();
    }

    public static AbsolutePath? GetLogsDumpDirectory(this IConfiguration configuration)
    {
        var path = configuration.GetRefValueOrDefault(LogsDumpDirectoryKey, null);
        return string.IsNullOrWhiteSpace(path) ? null : AbsolutePath.Create(path);
    }

    public static void SetLogsDumpDirectory(this IConfiguration configuration, AbsolutePath? logsDumpDirectory)
    {
        configuration[LogsDumpDirectoryKey] = logsDumpDirectory?.ToString();
    }

    public static AbsolutePath? GetLogsPath(this IConfiguration configuration)
    {
        var path = configuration.GetRefValueOrDefault(LogsPathKey, null);
        return string.IsNullOrWhiteSpace(path) ? null : AbsolutePath.Create(path);
    }

    public static void SetLogsPath(this IConfiguration configuration, AbsolutePath? logsPath)
    {
        configuration[LogsPathKey] = logsPath?.ToString();
    }
}
