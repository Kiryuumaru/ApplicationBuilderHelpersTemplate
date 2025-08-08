using AbsolutePathHelpers;
using ApplicationBuilderHelpers.Abstracts;
using ApplicationBuilderHelpers.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CommandLineParser.TypeParsers;

internal class LogLevelTypeParser : CommandTypeParser<LogLevel>
{
    public override LogLevel ParseValue(string? value, out string? validateError)
    {
        validateError = null;
        if (string.IsNullOrEmpty(value))
        {
            return LogLevel.Information;
        }
        try
        {
            return value switch
            {
                var s when string.Equals(s, nameof(LogLevel.Trace), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Trace,
                var s when string.Equals(s, nameof(LogLevel.Debug), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Debug,
                var s when string.Equals(s, nameof(LogLevel.Information), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Information,
                var s when string.Equals(s, nameof(LogLevel.Warning), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Warning,
                var s when string.Equals(s, nameof(LogLevel.Error), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Error,
                var s when string.Equals(s, nameof(LogLevel.Critical), StringComparison.InvariantCultureIgnoreCase) => LogLevel.Critical,
                var s when string.Equals(s, nameof(LogLevel.None), StringComparison.InvariantCultureIgnoreCase) => LogLevel.None,
                _ => throw new ArgumentException($"Invalid log level: {value}"),
            };
        }
        catch (ArgumentException ex)
        {
            validateError = ex.Message;
            return default;
        }
    }
}
