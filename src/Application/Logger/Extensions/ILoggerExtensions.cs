using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Application.Logger.Extensions;

public static class ILoggerExtensions
{
    public const string ServiceActionIdentifier = "LogServiceAction";
    public const string ServiceActionSeparatorIdentifier = "___";

    public static IDisposable? BeginScopeMap(this ILogger logger, Dictionary<string, object?> scopeMap)
    {
        return logger.BeginScope(scopeMap);
    }

    public static IDisposable? BeginScopeMap(this ILogger logger, string serviceName, string? serviceActionName = null, string? serviceCallerName = null, Dictionary<string, object?>? scopeMap = null)
    {
        scopeMap ??= [];
        serviceName = string.IsNullOrEmpty(serviceName) ? "0" : serviceName;
        serviceActionName = string.IsNullOrEmpty(serviceActionName) ? "0" : serviceActionName;
        serviceCallerName = string.IsNullOrEmpty(serviceCallerName) ? "0" : serviceCallerName;
        scopeMap[$"{ServiceActionIdentifier}{ServiceActionSeparatorIdentifier}{serviceName}{ServiceActionSeparatorIdentifier}{serviceActionName}{ServiceActionSeparatorIdentifier}{serviceCallerName}"] = true;
        return logger.BeginScope(scopeMap);
    }

    public static IDisposable? BeginScopeMap<TService>(this ILogger logger, string? serviceAction = null, [CallerMemberName] string? serviceCallerName = null, Dictionary<string, object?>? scopeMap = null)
    {
        var type = typeof(TService);
        return BeginScopeMap(logger, type.Name, serviceAction, serviceCallerName, scopeMap);
    }

    public static void Info(this ILogger logger, string message, params object?[] args)
    {
        logger.LogInformation(message, args);
    }

    public static void Warn(this ILogger logger, string message, params object?[] args)
    {
        logger.LogWarning(message, args);
    }

    public static void Error(this ILogger logger, string message, params object?[] args)
    {
        logger.LogError(message, args);
    }

    public static void Debug(this ILogger logger, string message, params object?[] args)
    {
        logger.LogDebug(message, args);
    }

    public static void Trace(this ILogger logger, string message, params object?[] args)
    {
        logger.LogTrace(message, args);
    }

    public static void Critical(this ILogger logger, string message, params object?[] args)
    {
        logger.LogCritical(message, args);
    }
}
