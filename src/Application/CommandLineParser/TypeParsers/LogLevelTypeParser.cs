using AbsolutePathHelpers;
using ApplicationBuilderHelpers.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CommandLineParser.TypeParsers;

internal class LogLevelTypeParser : ICommandTypeParser
{
    public Type Type { get; } = typeof(LogLevel);

    public string[] Choices { get; } =
        [
            nameof(LogLevel.Trace),
            nameof(LogLevel.Debug),
            nameof(LogLevel.Information),
            nameof(LogLevel.Warning),
            nameof(LogLevel.Error),
            nameof(LogLevel.Critical),
            nameof(LogLevel.None),
        ];

    public object? ParseToType(object? value)
    {
        var valueStr = value?.ToString();
        if (valueStr == null || string.IsNullOrEmpty(valueStr))
        {
            return LogLevel.Information;
        }
        return valueStr switch
        {
            var s when string.Equals(s, nameof(LogLevel.Trace), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Trace,
            var s when string.Equals(s, nameof(LogLevel.Debug), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Debug,
            var s when string.Equals(s, nameof(LogLevel.Information), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Information,
            var s when string.Equals(s, nameof(LogLevel.Warning), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Warning,
            var s when string.Equals(s, nameof(LogLevel.Error), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Error,
            var s when string.Equals(s, nameof(LogLevel.Critical), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Critical,
            var s when string.Equals(s, nameof(LogLevel.None), StringComparison.InvariantCultureIgnoreCase) => LogLevel.None,
            _ => throw new ArgumentException($"Invalid log level: {valueStr}"),
        };
    }

    public object? ParseFromType(object? value)
    {
        if (value == null || value is not LogLevel)
        {
            return nameof(LogLevel.Information);
        }
        return value switch
        {
            LogLevel.Trace => nameof(LogLevel.Trace),
            LogLevel.Debug => nameof(LogLevel.Debug),
            LogLevel.Information => nameof(LogLevel.Information),
            LogLevel.Warning => nameof(LogLevel.Warning),
            LogLevel.Error => nameof(LogLevel.Error),
            LogLevel.Critical => nameof(LogLevel.Critical),
            LogLevel.None => nameof(LogLevel.None),
            _ => throw new ArgumentException($"Invalid log level: {value}"),
        };
    }

    public bool Validate(object? value, [NotNullWhen(false)] out string? validateError)
    {
        validateError = null;
        if (value == null || value is not string valueStr || string.IsNullOrEmpty(valueStr))
        {
            return true;
        }
        if (Enum.TryParse(typeof(LogLevel), valueStr, true, out var result) && Enum.IsDefined(typeof(LogLevel), result))
        {
            return true;
        }
        validateError = $"Invalid log level: {valueStr}";
        return false;
    }
}
