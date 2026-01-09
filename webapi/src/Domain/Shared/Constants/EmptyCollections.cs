using System.Collections.ObjectModel;

namespace Domain.Shared.Constants;

/// <summary>
/// Shared empty collection instances to avoid redundant allocations.
/// </summary>
public static class EmptyCollections
{
    /// <summary>
    /// Empty dictionary of string to string (non-nullable values).
    /// </summary>
    public static IReadOnlyDictionary<string, string> StringStringDictionary { get; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(0, StringComparer.Ordinal));

    /// <summary>
    /// Empty dictionary of string to nullable string.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> StringNullableStringDictionary { get; } =
        new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>(0, StringComparer.Ordinal));
}
