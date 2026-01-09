namespace Domain.Shared.Exceptions;

/// <summary>
/// Exception thrown when attempting to use a reserved name.
/// </summary>
public sealed class ReservedNameException : DomainException
{
    public string? ReservedName { get; }

    public ReservedNameException(string message)
        : base(message)
    {
    }

    public ReservedNameException(string reservedName, string entityType)
        : base($"The name '{reservedName}' is reserved and cannot be used for {entityType}.")
    {
        ReservedName = reservedName;
    }

    public ReservedNameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
