using System.ComponentModel.DataAnnotations;

namespace Presentation.WebApi.Models.Requests;

/// <summary>
/// Request to get passkey authentication options.
/// </summary>
/// <param name="Username">Optional username to filter allowed credentials. If not provided, discoverable credentials will be used.</param>
public record PasskeyLoginOptionsRequest(
    string? Username
);

/// <summary>
/// Request to rename a passkey.
/// </summary>
/// <param name="Name">The new name for the passkey.</param>
public record PasskeyRenameRequest(
    [Required]
    [MaxLength(256)]
    string Name
);
