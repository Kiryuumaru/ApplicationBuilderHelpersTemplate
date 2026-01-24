using System.Text.Json;

namespace Application.Shared.Extensions;

public static class JsonSerializerExtension
{
    public static readonly JsonSerializerOptions CamelCaseOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static readonly JsonSerializerOptions CamelCaseNoIndentOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}
