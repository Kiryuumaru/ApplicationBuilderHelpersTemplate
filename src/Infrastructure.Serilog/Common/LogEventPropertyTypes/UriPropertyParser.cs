using Infrastructure.Serilog.Abstractions;

namespace Infrastructure.Serilog.Common.LogEventPropertyTypes;

internal class UriPropertyParser : LogEventPropertyParser<Uri>
{
    public static UriPropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        if (Uri.TryCreate(dataStr, UriKind.Absolute, out var result))
        {
            return result;
        }
        return null;
    }
}
