using System.Text.Json.Serialization;
using Domain.Grid.Models;

namespace Infrastructure.NetConduit.Serialization;

/// <summary>
/// Source-generated JSON context for NetConduit Grid protocol messages.
/// Used with DeltaTransit for efficient delta serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(GridMessage))]
public partial class NetConduitJsonContext : JsonSerializerContext
{
}
