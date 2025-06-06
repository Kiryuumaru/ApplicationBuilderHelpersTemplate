﻿using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using YamlDotNet.RepresentationModel;

namespace Application.Common.Extensions;

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
