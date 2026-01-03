using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Models.Requests;

/// <summary>
/// Request to link an email to the current user's account.
/// </summary>
public record LinkEmailRequest
{
    /// <summary>
    /// The email address to link to the account.
    /// </summary>
    [Required]
    [EmailAddress]
    public required string Email { get; init; }
}
