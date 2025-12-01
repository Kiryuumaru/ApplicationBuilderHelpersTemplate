using System.Collections.Generic;
using System.Linq;
using Domain.Authorization.Constants;
using Domain.Authorization.Models;

namespace Domain.UnitTests.Authorization;

public class PermissionTests
{
    private static Permission GetPortfolioAccountUpdatePermission()
    {
        var portfolio = Permissions.Api.Permissions.First(p => p.Identifier == "portfolio");
        var accounts = portfolio.Permissions.First(p => p.Identifier == "accounts");
        return accounts.Permissions.First(p => p.Identifier == "update");
    }

    [Fact]
    public void BuildPath_IncludesParameters()
    {
        var updatePermission = GetPortfolioAccountUpdatePermission();
        var path = updatePermission.BuildPath(new Dictionary<string, string?>
        {
            ["userId"] = "user-1",
            ["accountId"] = "account-9"
        });

        Assert.Equal("api:portfolio:[userId=user-1]:accounts:[accountId=account-9]:update", path);
    }

    [Fact]
    public void ParseIdentifier_NormalizesWhitespace()
    {
        var parsed = Permission.ParseIdentifier(" api : portfolio : accounts : update ");

        Assert.Equal("api:portfolio:accounts:update", parsed.Canonical);
        Assert.False(parsed.HasParameters);
    }

    [Fact]
    public void Traverse_Returns_All_Nodes()
    {
        var all = Permissions.GetAll();

        Assert.Contains(all, permission => permission.Identifier == "api");
        Assert.Contains(all, permission => permission.Identifier == "upload");
    }
}
