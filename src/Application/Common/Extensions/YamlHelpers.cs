using AbsolutePathHelpers;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Application.Common.Extensions;

public static class YamlHelpers
{
    public static T Deserialize<T>(string content, StaticContext staticContext)
    {
        return BuildStaticDeserializer(staticContext).Deserialize<T>(content);
    }

    [RequiresDynamicCode("Calls YamlDotNet.Serialization.DeserializerBuilder.DeserializerBuilder()")]
    public static T Deserialize<T>(string content)
    {
        return BuildDeserializer().Deserialize<T>(content);
    }

    public static T Deserialize<T>(AbsolutePath path, StaticContext staticContext)
    {
        using var reader = new StreamReader(path);
        return BuildStaticDeserializer(staticContext).Deserialize<T>(reader);
    }

    [RequiresDynamicCode("Calls YamlDotNet.Serialization.DeserializerBuilder.DeserializerBuilder()")]
    public static T Deserialize<T>(AbsolutePath path)
    {
        using var reader = new StreamReader(path);
        return BuildDeserializer().Deserialize<T>(reader);
    }

    public static string Serialize<T>(T obj, StaticContext staticContext)
    {
        return BuildStaticSerializer(staticContext).Serialize(obj);
    }

    [RequiresDynamicCode("Calls YamlDotNet.Serialization.SerializerBuilder.SerializerBuilder()")]
    public static string Serialize<T>(T obj)
    {
        return BuildSerializer().Serialize(obj);
    }

    public static void Serialize<T>(T obj, AbsolutePath path, StaticContext staticContext)
    {
        path.WriteAllText(BuildStaticSerializer(staticContext).Serialize(obj));
    }

    [RequiresDynamicCode("Calls YamlDotNet.Serialization.SerializerBuilder.SerializerBuilder()")]
    public static void Serialize<T>(T obj, AbsolutePath path)
    {
        path.WriteAllText(BuildSerializer().Serialize(obj));
    }

    public static string AddPadding(string[] yaml, int padding, bool skipFirst = true)
    {
        string pad = string.Join("", Enumerable.Repeat(" ", padding));
        for (int i = 0; i < yaml.Length; i++)
        {
            if (skipFirst && i == 0)
            {
                continue;
            }
            yaml[i] = $"{pad}{yaml[i]}";
        }
        return string.Join("\n", yaml);
    }

    public static string AddPadding(string yaml, int padding, bool skipFirst = true)
    {
        return AddPadding(yaml.Split('\n'), padding, skipFirst);
    }

    static IDeserializer BuildStaticDeserializer(StaticContext staticContext)
    {
        return new StaticDeserializerBuilder(staticContext)
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    [RequiresDynamicCode("Calls YamlDotNet.Serialization.DeserializerBuilder.DeserializerBuilder()")]
    static IDeserializer BuildDeserializer()
    {
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    static ISerializer BuildStaticSerializer(StaticContext staticContext)
    {
        return new StaticSerializerBuilder(staticContext)
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    [RequiresDynamicCode("Calls YamlDotNet.Serialization.SerializerBuilder.SerializerBuilder()")]
    static ISerializer BuildSerializer()
    {
        return new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }
}
