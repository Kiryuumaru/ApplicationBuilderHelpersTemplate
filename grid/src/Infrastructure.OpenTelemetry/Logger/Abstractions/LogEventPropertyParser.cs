using Infrastructure.OpenTelemetry.Logger.Interfaces;

namespace Infrastructure.OpenTelemetry.Logger.Abstractions;

internal abstract class LogEventPropertyParser<T> : ILogEventPropertyParser
{
    public string TypeIdentifier => typeof(T).Name;

    public abstract object? Parse(string? dataStr);
}
