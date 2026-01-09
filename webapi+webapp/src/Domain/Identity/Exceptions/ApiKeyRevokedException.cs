namespace Domain.Identity.Exceptions;

/// <summary>
/// Exception thrown when an API key has been revoked.
/// </summary>
public sealed class ApiKeyRevokedException : Exception
{
    public ApiKeyRevokedException(Guid keyId)
        : base($"API key with ID '{keyId}' has been revoked.")
    {
        KeyId = keyId;
    }

    /// <summary>
    /// The ID of the revoked API key.
    /// </summary>
    public Guid KeyId { get; }
}
