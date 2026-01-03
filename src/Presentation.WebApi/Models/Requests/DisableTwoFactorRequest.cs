using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Models.Requests;

/// <summary>
/// Request model for disabling two-factor authentication.
/// </summary>
public sealed record DisableTwoFactorRequest
{
    /// <summary>
    /// The user's password to confirm the action.
    /// </summary>
    [Required]
    public required string Password { get; init; }
}
