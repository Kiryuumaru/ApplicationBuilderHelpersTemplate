using Application.Logger.Abstractions;

namespace Application.Logger.Common.LogEventPropertyTypes;

internal class ShortPropertyParser : LogEventPropertyParser<short>
{
    public static ShortPropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        if (short.TryParse(dataStr, out var result))
        {
            return result;
        }
        return null;
    }
}
