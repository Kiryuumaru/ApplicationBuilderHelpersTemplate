using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Application.Logger.Extensions;

public static class ILoggerExtensions
{
    public const string SourceContextActionIdentifier = "SourceContextAction";
    public const string SourceContextActionsIdentifier = "SourceContextActions";
    public const string SourceContextActionSeparator = "___";
    public const string SourceContextActionIdentifierAndSeparator = $"{SourceContextActionIdentifier}{SourceContextActionSeparator}";

    public static IDisposable? BeginScopeMap(this ILogger logger, [CallerMemberName] string? contextAction = null, Dictionary<string, object?>? scopeMap = null)
    {
        scopeMap ??= [];
        if (!string.IsNullOrEmpty(contextAction))
        {
            scopeMap[$"{SourceContextActionIdentifierAndSeparator}{contextAction}"] = true;
        }
        return logger.BeginScope(scopeMap);
    }
}
