using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Models.Requests;

/// <summary>
/// Request to change the current user's username.
/// </summary>
public record ChangeUsernameRequest
{
    /// <summary>
    /// The new username.
    /// </summary>
    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    public required string Username { get; init; }
}
