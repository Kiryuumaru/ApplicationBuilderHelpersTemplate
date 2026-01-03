namespace Application.Authorization.Models;

/// <summary>
/// Result of token validation.
/// </summary>
public sealed record TokenValidationResult
{
    /// <summary>
    /// Whether the token is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// The user ID from the token, if valid.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// The session ID from the token, if present.
    /// </summary>
    public Guid? SessionId { get; init; }

    /// <summary>
    /// The roles from the token, if valid.
    /// </summary>
    public IReadOnlyCollection<string>? Roles { get; init; }

    /// <summary>
    /// Error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static TokenValidationResult Success(Guid userId, Guid? sessionId, IReadOnlyCollection<string> roles) => new()
    {
        IsValid = true,
        UserId = userId,
        SessionId = sessionId,
        Roles = roles
    };

    public static TokenValidationResult Failed(string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage
    };
}
