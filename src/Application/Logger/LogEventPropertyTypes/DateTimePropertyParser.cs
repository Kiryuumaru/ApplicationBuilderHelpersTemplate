using Application.Logger.Abstractions;

namespace Application.Logger.LogEventPropertyTypes;

internal class DateTimePropertyParser : LogEventPropertyParser<DateTime>
{
    public static DateTimePropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        if (DateTime.TryParse(dataStr, out var result))
        {
            return result;
        }
        return null;
    }
}
