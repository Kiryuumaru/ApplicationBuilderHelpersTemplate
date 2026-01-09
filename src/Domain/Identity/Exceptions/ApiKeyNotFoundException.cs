namespace Domain.Identity.Exceptions;

/// <summary>
/// Exception thrown when an API key is not found.
/// </summary>
public sealed class ApiKeyNotFoundException : Exception
{
    public ApiKeyNotFoundException(Guid keyId)
        : base($"API key with ID '{keyId}' was not found.")
    {
        KeyId = keyId;
    }

    /// <summary>
    /// The ID of the API key that was not found.
    /// </summary>
    public Guid KeyId { get; }
}
