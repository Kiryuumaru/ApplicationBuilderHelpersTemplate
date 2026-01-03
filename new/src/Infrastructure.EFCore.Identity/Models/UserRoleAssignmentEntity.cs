namespace Infrastructure.EFCore.Identity.Models;

/// <summary>
/// Entity for persisting user role assignments with parameter values.
/// </summary>
public class UserRoleAssignmentEntity
{
    public required Guid UserId { get; set; }
    public required Guid RoleId { get; set; }
    /// <summary>
    /// JSON-serialized dictionary of parameter values for the role assignment.
    /// For example: {"roleUserId": "12345678-1234-1234-1234-123456789012"}
    /// </summary>
    public string? ParameterValuesJson { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
}
