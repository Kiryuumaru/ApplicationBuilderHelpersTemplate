using Domain.Authorization.Models;
using Domain.Authorization.ValueObjects;

namespace Domain.UnitTests.Authorization;

public class RoleTests
{
    [Fact]
    public void AssignPermission_AddsGrantOnce()
    {
        var role = Role.Create("analyst", "Analyst");
        var grant = RolePermissionTemplate.Create("api:portfolio:accounts:list");

        Assert.True(role.AssignPermission(grant));
        Assert.False(role.AssignPermission(grant));
        Assert.Single(role.PermissionGrants);
    }

    [Fact]
    public void ReplacePermissions_ReplacesExistingSet()
    {
        var role = Role.Create("operator", "Operator");
        role.AssignPermission(RolePermissionTemplate.Create("api:user:profile:read"));

        var updated = new[]
        {
            RolePermissionTemplate.Create("api:portfolio:accounts:list"),
            RolePermissionTemplate.Create("api:portfolio:positions:close")
        };

        role.ReplacePermissions(updated);

        Assert.Equal(2, role.PermissionGrants.Count);
        Assert.Contains(role.PermissionGrants, grant => grant.IdentifierTemplate == "api:portfolio:positions:close");
        Assert.DoesNotContain(role.PermissionGrants, grant => grant.IdentifierTemplate == "api:user:profile:read");
    }
}
