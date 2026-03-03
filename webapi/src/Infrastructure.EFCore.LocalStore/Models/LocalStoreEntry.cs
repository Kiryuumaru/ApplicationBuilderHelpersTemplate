namespace Infrastructure.EFCore.LocalStore.Models;

public class LocalStoreEntry
{
    public required string Group { get; set; }
    public required string Id { get; set; }
    public string? Data { get; set; }
}
