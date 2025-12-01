using Application.Authorization.Models;
using Application.Authorization.Services;
using Domain.Authorization.Constants;
using RolesConstants = Domain.Authorization.Constants.Roles;

namespace Application.UnitTests.Authorization;

public class RoleServiceTests
{
    [Fact]
    public async Task EnsureSystemRolesAsync_SeedsDefaultDefinitions()
    {
        var repository = new InMemoryRoleRepository();
        var service = new RoleService(repository);

        await service.EnsureSystemRolesAsync(CancellationToken.None);
        var roles = await service.ListAsync(CancellationToken.None);

        Assert.Contains(roles, role => role.Code == RolesConstants.Admin.Code);
        Assert.Contains(roles, role => role.Code == RolesConstants.User.Code);
    }

    [Fact]
    public async Task CreateRoleAsync_PreventsDuplicateCodes()
    {
        var repository = new InMemoryRoleRepository();
        var service = new RoleService(repository);

        var descriptor = new RoleDescriptor(
            Code: "portfolio_manager",
            Name: "Portfolio Manager",
            Description: "Manages portfolios",
            IsSystemRole: false,
            PermissionTemplates:
            [
                new RolePermissionTemplateDescriptor("api:portfolio:[userId=user-123]:accounts:list")
            ]);

        await service.CreateRoleAsync(descriptor, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRoleAsync(descriptor, CancellationToken.None));
    }

    [Fact]
    public async Task ReplacePermissionsAsync_UpdatesRoleGrants()
    {
        var repository = new InMemoryRoleRepository();
        var service = new RoleService(repository);

        var descriptor = new RoleDescriptor(
            Code: "analyst",
            Name: "Read Only Analyst",
            Description: null,
            IsSystemRole: false,
            PermissionTemplates:
            [
                new RolePermissionTemplateDescriptor("api:market:assets:list")
            ]);

        var role = await service.CreateRoleAsync(descriptor, CancellationToken.None);

        role = await service.ReplacePermissionsAsync(
            role.Id,
            [new RolePermissionTemplateDescriptor(Permissions.RootReadIdentifier)],
            CancellationToken.None);

        Assert.Contains(Permissions.RootReadIdentifier, role.GetPermissionIdentifiers());
        Assert.Single(role.GetPermissionIdentifiers());
    }
}
