using System.Text.Json;

namespace Application.Common.Extensions;

public static class JsonElementExtensions
{
    public static JsonElement GetPropertyOrThrow(this JsonElement element, params string[] propertyNames)
    {
        if (propertyNames == null || propertyNames.Length == 0)
            throw new ArgumentException("At least one property name must be provided.", nameof(propertyNames));

        JsonElement currentElement = element;
        string currentKey = "@root";

        foreach (var propertyName in propertyNames)
        {
            if (currentElement.TryGetProperty(propertyName, out var nextElement))
            {
                currentElement = nextElement;
            }
            else
            {
                throw new KeyNotFoundException($"Property '{propertyName}' was not found in the JSON document '{currentKey}'.");
            }
            currentKey = currentKey + "." + propertyName;
        }

        return currentElement;
    }
}
