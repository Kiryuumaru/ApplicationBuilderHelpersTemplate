using System;
using Domain.Authorization.ValueObjects;
using Domain.Shared.Exceptions;

namespace Domain.UnitTests.Authorization;

public class RolePermissionTemplateTests
{
    [Fact]
    public void Create_InferParametersWhenListOmitted()
    {
        var template = "api:[userId={roleUserId}]:_read";

        var grant = RolePermissionTemplate.Create(template);

        Assert.Contains("roleUserId", grant.RequiredParameters, StringComparer.Ordinal);
    }

    [Fact]
    public void Create_ThrowsWhenTemplateReceivesUnusedParameters()
    {
        var template = "api:portfolio:positions:read";

        var exception = Assert.Throws<DomainException>(
            () => RolePermissionTemplate.Create(template, ["portfolioId"]));

        Assert.Contains("does not allow parameters", exception.Message);
    }

    [Fact]
    public void Create_ThrowsWhenParameterListDoesNotMatchTemplate()
    {
        var template = "api:portfolio:[portfolioId={portfolioId}]:positions:[accountId={accountId}]:read";

        var exception = Assert.Throws<DomainException>(
            () => RolePermissionTemplate.Create(template, ["portfolioId"]));

        Assert.Contains("missing required parameters", exception.Message);
    }
}
