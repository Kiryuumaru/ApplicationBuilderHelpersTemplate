namespace Presentation.WebApi.Models.Responses;

/// <summary>
/// Response model for credential validation result.
/// </summary>
public sealed record ValidateCredentialsResponse
{
    /// <summary>
    /// Whether the credentials are valid.
    /// </summary>
    public required bool Valid { get; init; }

    /// <summary>
    /// Error message if validation failed.
    /// </summary>
    public string? Error { get; init; }
}
