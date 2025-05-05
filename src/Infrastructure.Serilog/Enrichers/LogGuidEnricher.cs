using Application.Configuration.Extensions;
using Application.Logger.Extensions;
using Infrastructure.Serilog.Abstractions;
using Infrastructure.Serilog.Common.LogEventPropertyTypes;
using Infrastructure.Serilog.Models;
using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Infrastructure.Serilog.Enrichers;

internal class LogGuidEnricher(IConfiguration configuration) : ILogEventEnricher
{
    private readonly IConfiguration _configuration = configuration;

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
            _runtimeGuid = _configuration.GetRuntimeGuid();
        }

        AddProperty(evt, "EventGuid", Guid.NewGuid(), false);
        AddProperty(evt, "RuntimeGuid", _runtimeGuid.Value, false);
        if (!_hasHeadRuntimeLogs)
        {
            AddProperty(evt, "IsHeadLog", true, false);
            _hasHeadRuntimeLogs = true;
        }

        List<LogJourney> logJourney = [];

        foreach (var prop in evt.Properties)
        {
            if (prop.Key.StartsWith(ILoggerExtensions.ServiceActionIdentifier))
            {
                var serviceAction = prop.Key.Split(ILoggerExtensions.ServiceActionSeparatorIdentifier);
                var serviceName = serviceAction[1];
                var serviceActionName = serviceAction[2];
                var serviceCallerName = serviceAction[3];

                logJourney.Add(new LogJourney()
                {
                    ServiceName = serviceName,
                    ServiceActionName = serviceActionName,
                    ServiceCallerName = serviceCallerName
                });
            }
        }

        if (logJourney.Count > 0)
        {
            var lastLogJourney = logJourney.Last();
            SequenceValue sequenceValue = new(logJourney.Select(i => new StructureValue(
                [
                    new LogEventProperty("LogServiceName", string.IsNullOrEmpty(i.ServiceName) || i.ServiceName == "0" ? ScalarValue.Null : new ScalarValue(i.ServiceName)),
                    new LogEventProperty("LogServiceActionName", string.IsNullOrEmpty(i.ServiceActionName) || i.ServiceActionName == "0" ? ScalarValue.Null : new ScalarValue(i.ServiceActionName)),
                    new LogEventProperty("LogServiceCallerName", string.IsNullOrEmpty(i.ServiceCallerName) || i.ServiceCallerName == "0" ? ScalarValue.Null : new ScalarValue(i.ServiceCallerName))
                ])));

            evt.AddOrUpdateProperty(new("LogJourney", sequenceValue));

            if (!string.IsNullOrEmpty(lastLogJourney.ServiceName) && lastLogJourney.ServiceName != "0")
                evt.AddOrUpdateProperty(new("LogServiceName", new ScalarValue(lastLogJourney.ServiceName)));
            if (!string.IsNullOrEmpty(lastLogJourney.ServiceActionName) && lastLogJourney.ServiceActionName != "0")
                evt.AddOrUpdateProperty(new("LogServiceActionName", new ScalarValue(lastLogJourney.ServiceActionName)));
            if (!string.IsNullOrEmpty(lastLogJourney.ServiceCallerName) && lastLogJourney.ServiceCallerName != "0")
                evt.AddOrUpdateProperty(new("LogServiceCallerName", new ScalarValue(lastLogJourney.ServiceCallerName)));
        }

        EnrichLogTypings(evt);
    }

    private void EnrichLogTypings(LogEvent evt)
    {
        StructureValue? realValueTypeIdentifierKey = evt.Properties.GetValueOrDefault(ValueTypeMapIdentifier) as StructureValue;
        Dictionary<string, LogEventProperty> logTypings = realValueTypeIdentifierKey?.Properties?.ToDictionary(i => i.Name, i => i) ?? [];

        foreach (var prop in evt.Properties.ToDictionary())
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
                evt.AddOrUpdateProperty(newValueTypingProp);

                logTypings[prop.Key] = new(prop.Key, new ScalarValue(realValueLogTyping));
            }
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