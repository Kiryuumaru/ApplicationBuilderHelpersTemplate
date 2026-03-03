namespace Infrastructure.EFCore.Server.Identity.Models;

public class UserRoleAssignmentEntity
{
    public required Guid UserId { get; set; }
    public required Guid RoleId { get; set; }
    public string? ParameterValuesJson { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
}
