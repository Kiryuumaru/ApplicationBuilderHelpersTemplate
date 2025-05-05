using AbsolutePathHelpers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Application.Common.Extensions;

public static class YamlHelpers
{
    static readonly IDeserializer yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    static readonly ISerializer yamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static T DeserializeString<T>(string content)
    {
        T obj;
        try
        {
            obj = yamlDeserializer.Deserialize<T>(content);
        }
        catch (Exception e)
        {
            throw new Exception($"yaml content is not in correct format: {e.InnerException?.Message ?? e.Message}");
        }
        return obj;
    }

    public static T DeserializeFile<T>(AbsolutePath path)
    {
        T obj;
        try
        {
            using var reader = new StreamReader(path);
            obj = yamlDeserializer.Deserialize<T>(reader);
        }
        catch (Exception e)
        {
            throw new Exception($"\"{path}\" is not in correct format: {e.InnerException?.Message ?? e.Message}");
        }
        return obj;
    }

    public static void SerializeFile<T>(T obj, AbsolutePath path)
    {
        try
        {
            path.WriteAllText(yamlSerializer.Serialize(obj));
        }
        catch (Exception e)
        {
            throw new Exception($"\"{path}\" is not in correct format: {e.InnerException?.Message ?? e.Message}");
        }
    }

    public static string SerializeString<T>(T obj)
    {
        try
        {
            return yamlSerializer.Serialize(obj);
        }
        catch (Exception e)
        {
            throw new Exception($"yaml content is not in correct format: {e.InnerException?.Message ?? e.Message}");
        }
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
}
