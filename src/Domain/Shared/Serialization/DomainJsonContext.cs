using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.AppEnvironment.Models;
using Domain.Authorization.Enums;
using Domain.Authorization.Models;
using Domain.Identity.Models;
using Domain.Identity.ValueObjects;
using Domain.Shared.Serialization.Converters;

namespace Domain.Shared.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    Converters = new[]
    {
        typeof(PermissionAccessCategoryJsonConverter)
    })]
[JsonSerializable(typeof(AppEnvironment.Models.AppEnvironment))]
[JsonSerializable(typeof(Permission))]
[JsonSerializable(typeof(Permission.ParsedIdentifier))]
[JsonSerializable(typeof(PermissionAccessCategory))]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(UserSession))]
[JsonSerializable(typeof(UserPermissionGrant))]
[JsonSerializable(typeof(UserRoleAssignment))]
[JsonSerializable(typeof(UserIdentityLink))]
public partial class DomainJsonContext : JsonSerializerContext
{
    public static JsonSerializerOptions CreateOptions(JsonSerializerOptions? baseOptions = null)
    {
        var options = baseOptions is null
            ? new JsonSerializerOptions(JsonSerializerDefaults.Web)
            : new JsonSerializerOptions(baseOptions);

        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        AddConverterIfMissing<PermissionAccessCategoryJsonConverter>(options);

        if (!options.TypeInfoResolverChain.Contains(Default))
        {
            options.TypeInfoResolverChain.Insert(0, Default);
        }

        return options;
    }

    private static void AddConverterIfMissing<TConverter>(JsonSerializerOptions options)
        where TConverter : JsonConverter, new()
    {
        foreach (var converter in options.Converters)
        {
            if (converter is TConverter)
            {
                return;
            }
        }

        options.Converters.Add(new TConverter());
    }
}
