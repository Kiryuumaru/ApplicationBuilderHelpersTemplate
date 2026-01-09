using Application.Logger.Abstractions;

namespace Application.Logger.LogEventPropertyTypes;

internal class GuidPropertyParser : LogEventPropertyParser<Guid>
{
    public static GuidPropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        if (Guid.TryParse(dataStr, out var result))
        {
            return result;
        }
        return null;
    }
}
