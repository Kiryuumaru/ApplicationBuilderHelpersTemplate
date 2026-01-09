namespace Domain.Shared.Exceptions;

/// <summary>
/// Exception thrown when attempting to create an entity that already exists.
/// </summary>
public sealed class DuplicateEntityException : DomainException
{
    public string? EntityType { get; }
    public string? EntityIdentifier { get; }

    public DuplicateEntityException(string message)
        : base(message)
    {
    }

    public DuplicateEntityException(string entityType, string entityIdentifier)
        : base($"{entityType} '{entityIdentifier}' already exists.")
    {
        EntityType = entityType;
        EntityIdentifier = entityIdentifier;
    }

    public DuplicateEntityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
