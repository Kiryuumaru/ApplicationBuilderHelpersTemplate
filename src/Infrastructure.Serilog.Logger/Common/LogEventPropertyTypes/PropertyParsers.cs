using Infrastructure.Serilog.Logger.Abstractions;

namespace Infrastructure.Serilog.Logger.Common.LogEventPropertyTypes;

internal class BooleanPropertyParser : LogEventPropertyParser<bool>
{
    public static BooleanPropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        if (bool.TryParse(dataStr, out var result))
        {
            return result;
        }
        return null;
    }
}

internal class StringPropertyParser : LogEventPropertyParser<string>
{
    public static StringPropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        return dataStr;
    }
}

internal class IntPropertyParser : LogEventPropertyParser<int>
{
    public static IntPropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        if (int.TryParse(dataStr, out var result))
        {
            return result;
        }
        return null;
    }
}

internal class LongPropertyParser : LogEventPropertyParser<long>
{
    public static LongPropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        if (long.TryParse(dataStr, out var result))
        {
            return result;
        }
        return null;
    }
}

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

internal class DateTimeOffsetPropertyParser : LogEventPropertyParser<DateTimeOffset>
{
    public static DateTimeOffsetPropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        if (DateTimeOffset.TryParse(dataStr, out var result))
        {
            return result;
        }
        return null;
    }
}

internal class UriPropertyParser : LogEventPropertyParser<Uri>
{
    public static UriPropertyParser Default { get; } = new();

    public override object? Parse(string? dataStr)
    {
        if (Uri.TryCreate(dataStr, UriKind.RelativeOrAbsolute, out var result))
        {
            return result;
        }
        return null;
    }
}