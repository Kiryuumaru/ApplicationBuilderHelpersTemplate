using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Application.Common.Extensions;

public static class YamlJsonConverter
{
    public static async ValueTask<JsonDocument[]> ConvertToJson(string yamlContent)
    {
        static JsonNode? ConvertYamlNodeToObject(YamlNode node)
        {
            switch (node)
            {
                case YamlScalarNode scalarNode:
                    return JsonValue.Create(scalarNode.Value);

                case YamlSequenceNode sequenceNode:
                    var jsonArray = new JsonArray();
                    foreach (var child in sequenceNode.Children)
                    {
                        jsonArray.Add(ConvertYamlNodeToObject(child));
                    }
                    return jsonArray;

                case YamlMappingNode mappingNode:
                    var jsonObject = new JsonObject();
                    foreach (var entry in mappingNode.Children)
                    {
                        if (entry.Key is YamlScalarNode keyNode)
                        {
                            jsonObject.Add(keyNode.Value!, ConvertYamlNodeToObject(entry.Value));
                        }
                    }
                    return jsonObject;

                default:
                    return null;
            }
        }

        var input = new StringReader(yamlContent);

        var yaml = new YamlStream();
        yaml.Load(input);

        List<JsonDocument> jsonDocuments = [];

        foreach (var yamlDoc in yaml.Documents)
        {
            var rootNode = yamlDoc.RootNode;

            var jsonObject = ConvertYamlNodeToObject(rootNode);

            using Stream jsonStream = new MemoryStream();
            using Utf8JsonWriter jsonWriter = new(jsonStream);

            jsonObject?.WriteTo(jsonWriter);
            await jsonWriter.FlushAsync();

            jsonStream.Seek(0, SeekOrigin.Begin);

            jsonDocuments.Add(await JsonDocument.ParseAsync(jsonStream));
        }

        return [.. jsonDocuments];
    }

    public static async ValueTask<string> ConvertToYaml(JsonDocument[] jsonDocs)
    {
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
        ISerializer serializer = new SerializerBuilder()
            .Build();
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IDE0079 // Remove unnecessary suppression

        static object? ConvertJsonElementToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dictionary = new Dictionary<string, object?>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dictionary[property.Name] = ConvertJsonElementToObject(property.Value);
                    }
                    return dictionary;

                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonElementToObject(item));
                    }
                    return list;

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long longValue))
                        return longValue;
                    return element.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return null;
            }
        }

        if (jsonDocs.Length == 0)
            return await ValueTask.FromResult(string.Empty);


        var yamlStrings = new List<string>();

        foreach (var jsonDoc in jsonDocs)
        {
            // Convert JsonDocument to a dynamic object structure
            var obj = ConvertJsonElementToObject(jsonDoc.RootElement);

            // Serialize to YAML
            var yamlString = serializer.Serialize(obj);
            yamlStrings.Add(yamlString);
        }

        // Join all documents with separators
        return await ValueTask.FromResult(string.Join("---\n", yamlStrings));
    }
}