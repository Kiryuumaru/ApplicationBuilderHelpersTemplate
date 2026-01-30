using System.Text.Json.Serialization;
using Domain.HelloWorld.Entities;
using Domain.HelloWorld.Events;

namespace Domain.Serialization;

/// <summary>
/// Source-generated JSON context for Domain types.
/// Add [JsonSerializable(typeof(T))] for each type that needs serialization.
/// Supports Native AOT compilation.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
// HelloWorld Feature
[JsonSerializable(typeof(HelloWorldEntity))]
[JsonSerializable(typeof(HelloWorldCreatedEvent))]
public partial class DomainJsonContext : JsonSerializerContext
{
}
