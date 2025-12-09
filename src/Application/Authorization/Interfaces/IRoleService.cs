using Application.Authorization.Models;
using Domain.Authorization.Models;

namespace Application.Authorization.Interfaces;

public interface IRoleService
{
    Task<Role> CreateRoleAsync(RoleDescriptor descriptor, CancellationToken cancellationToken);

    Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Role>> ListAsync(CancellationToken cancellationToken);

    Task<Role> ReplacePermissionsAsync(Guid roleId, IEnumerable<RolePermissionTemplateDescriptor> permissionTemplates, CancellationToken cancellationToken);

    Task<Role> UpdateMetadataAsync(Guid roleId, string name, string? description, CancellationToken cancellationToken);
}
