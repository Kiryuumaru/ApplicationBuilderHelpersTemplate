namespace Infrastructure.EFCore.LocalStore.Models;

/// <summary>
/// Entity for local key-value storage.
/// </summary>
public class LocalStoreEntry
{
    public required string Group { get; set; }
    public required string Id { get; set; }
    public string? Data { get; set; }
}
