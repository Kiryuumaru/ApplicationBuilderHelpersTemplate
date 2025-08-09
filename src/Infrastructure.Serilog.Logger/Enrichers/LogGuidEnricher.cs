using Application.Common.Configuration.Extensions;
using Application.Logger.Extensions;
using Infrastructure.Serilog.Logger.Abstractions;
using Infrastructure.Serilog.Logger.Common.LogEventPropertyTypes;
using Infrastructure.Serilog.Logger.Models;
using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Infrastructure.Serilog.Logger.Enrichers;

internal class LogGuidEnricher(IConfiguration configuration, Dictionary<string, object?>? scopeMap) : ILogEventEnricher
{
    private const string ValueTypeMapIdentifier = "ValueTypeMap";
    private readonly Dictionary<string, ILogEventPropertyParser> _propertyParserMap = new()
    {
        [BooleanPropertyParser.Default.TypeIdentifier] = BooleanPropertyParser.Default,
        [DateTimeOffsetPropertyParser.Default.TypeIdentifier] = DateTimeOffsetPropertyParser.Default,
        [DateTimePropertyParser.Default.TypeIdentifier] = DateTimePropertyParser.Default,
        [GuidPropertyParser.Default.TypeIdentifier] = GuidPropertyParser.Default,
        [IntPropertyParser.Default.TypeIdentifier] = IntPropertyParser.Default,
        [LongPropertyParser.Default.TypeIdentifier] = LongPropertyParser.Default,
        [ShortPropertyParser.Default.TypeIdentifier] = ShortPropertyParser.Default,
        [StringPropertyParser.Default.TypeIdentifier] = StringPropertyParser.Default,
        [UriPropertyParser.Default.TypeIdentifier] = UriPropertyParser.Default,
    };

    private static bool _hasHeadRuntimeLogs = false;
    private static Guid? _runtimeGuid = null;

    public void Enrich(LogEvent evt, ILogEventPropertyFactory _)
    {
        if (_runtimeGuid == null)
        {
            _runtimeGuid = configuration.GetRuntimeGuid();
        }

        AddProperty(evt, "EventGuid", Guid.NewGuid(), false);
        AddProperty(evt, "RuntimeGuid", _runtimeGuid.Value, false);
        if (!_hasHeadRuntimeLogs)
        {
            AddProperty(evt, "IsHeadLog", true, false);
            _hasHeadRuntimeLogs = true;
        }

        if (scopeMap != null)
        {
            foreach (var prop in scopeMap ?? [])
            {
                AddProperty(evt, prop.Key, prop.Value, addAndReplace: true);
            }
        }

        EnrichLogSourceContext(evt);
        EnrichLogTypings(evt);
    }

    private static void EnrichLogSourceContext(LogEvent evt)
    {
        List<ScalarValue> sourceContexts = [];
        List<string> keysToRemove = [];
        string? lastSourceContext = null;

        foreach (var prop in evt.Properties)
        {
            if (prop.Key.StartsWith(ILoggerExtensions.SourceContextActionIdentifierAndSeparator))
            {
                var split = prop.Key.Split(ILoggerExtensions.SourceContextActionSeparator);
                if (split.Length <= 1)
                {
                    continue;
                }
                lastSourceContext = split[1];
                sourceContexts.Add(new ScalarValue(split[1]));
                keysToRemove.Add(prop.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            RemoveProperty(evt, key);
        }

        if (lastSourceContext != null)
        {
            evt.AddOrUpdateProperty(new LogEventProperty(ILoggerExtensions.SourceContextActionsIdentifier, new SequenceValue(sourceContexts)));
            AddProperty(evt, ILoggerExtensions.SourceContextActionIdentifier, lastSourceContext, addAndReplace: true);
        }
    }

    private void EnrichLogTypings(LogEvent evt)
    {
        StructureValue? realValueTypeIdentifierKey = evt.Properties.GetValueOrDefault(ValueTypeMapIdentifier) as StructureValue;
        Dictionary<string, LogEventProperty> logTypings = realValueTypeIdentifierKey?.Properties?.ToDictionary(i => i.Name, i => i) ?? [];
        List<LogEventProperty> propertiesToAdd = [];

        foreach (var prop in evt.Properties)
        {
            object? value = (prop.Value as ScalarValue)?.Value;
            Type? valueType = value == null ? null : GetUnderlyingType(value);

            if (valueType == null)
            {
                continue;
            }

            string? realValueLogTyping = null;
            if (logTypings.TryGetValue(prop.Key, out var existingValueLogTyping))
            {
                realValueLogTyping = (existingValueLogTyping.Value as ScalarValue)?.Value?.ToString()!;
            }

            realValueLogTyping ??= valueType.Name;

            if (!string.IsNullOrEmpty(realValueLogTyping) && _propertyParserMap.TryGetValue(realValueLogTyping, out var logEventPropertyParser))
            {
                LogEventProperty newValueTypingProp = new(prop.Key, new ScalarValue(logEventPropertyParser.Parse(value?.ToString())));
                propertiesToAdd.Add(newValueTypingProp);

                logTypings[prop.Key] = new(prop.Key, new ScalarValue(realValueLogTyping));
            }
        }

        foreach (var prop in propertiesToAdd)
        {
            evt.AddOrUpdateProperty(prop);
        }

        evt.AddOrUpdateProperty(new LogEventProperty(ValueTypeMapIdentifier, new StructureValue(logTypings.Values)));
    }

    private static void AddProperty(LogEvent evt, string key, object? value, bool addAndReplace)
    {
        LogEventProperty logEventProperty = new(key, new ScalarValue(value));

        if (addAndReplace)
        {
            evt.AddOrUpdateProperty(logEventProperty);
        }
        else
        {
            evt.AddPropertyIfAbsent(logEventProperty);
        }
    }

    private static void RemoveProperty(LogEvent evt, string key)
    {
        evt.RemovePropertyIfPresent(key);
    }

    private static Type GetUnderlyingType(object obj)
    {
        Type valueType = obj.GetType();
        while (true)
        {
            var underlyingType = Nullable.GetUnderlyingType(valueType);
            if (underlyingType == null)
            {
                break;
            }
            valueType = underlyingType;
        }
        return valueType;
    }
}
