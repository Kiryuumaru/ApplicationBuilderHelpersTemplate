namespace Infrastructure.EFCore.Server.Identity.Models;

/// <summary>
/// EF Core entity for storing Role data including ScopeTemplates as JSON.
/// </summary>
internal sealed class RoleEntity
{
    public Guid Id { get; set; }
    public Guid? RevId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Description { get; set; }
    
    /// <summary>
    /// JSON serialized scope templates.
    /// </summary>
    public string? ScopeTemplatesJson { get; set; }
}
