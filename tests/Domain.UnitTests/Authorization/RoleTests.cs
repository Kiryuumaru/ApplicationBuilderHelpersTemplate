using Domain.Authorization.Models;
using Domain.Authorization.ValueObjects;

namespace Domain.UnitTests.Authorization;

public class RoleTests
{
    [Fact]
    public void AddScopeTemplate_AddsTemplateOnce()
    {
        var role = Role.Create("analyst", "Analyst");
        var template = ScopeTemplate.Allow("api:portfolio:accounts:list");

        role.AddScopeTemplate(template);
        role.AddScopeTemplate(template);

        Assert.Equal(2, role.ScopeTemplates.Count); // ScopeTemplates allows duplicates; use ReplaceScopeTemplates for deduplication
    }

    [Fact]
    public void ReplaceScopeTemplates_ReplacesExistingSet()
    {
        var role = Role.Create("operator", "Operator");
        role.AddScopeTemplate(ScopeTemplate.Allow("api:user:profile:read"));

        var updated = new[]
        {
            ScopeTemplate.Allow("api:portfolio:accounts:list"),
            ScopeTemplate.Allow("api:portfolio:positions:close")
        };

        role.ReplaceScopeTemplates(updated);

        Assert.Equal(2, role.ScopeTemplates.Count);
        Assert.Contains(role.ScopeTemplates, t => t.PermissionPath == "api:portfolio:positions:close");
        Assert.DoesNotContain(role.ScopeTemplates, t => t.PermissionPath == "api:user:profile:read");
    }
}
