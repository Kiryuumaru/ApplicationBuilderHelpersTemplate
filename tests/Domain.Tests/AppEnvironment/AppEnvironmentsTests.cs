using System;
using Domain.AppEnvironment.Constants;

namespace Domain.Tests.AppEnvironment;

public class AppEnvironmentsTests
{
    [Theory]
    [InlineData("alpha", "Development", "dev")]
    [InlineData("beta", "Staging", "stg")]
    [InlineData("rc", "Preproduction", "pre")]
    [InlineData("prod", "Production", "prod")]
    public void GetByTag_ReturnsExpectedEnvironment(string tag, string name, string shortName)
    {
        var environment = AppEnvironments.GetByTag(tag);

        Assert.Equal(name, environment.Environment);
        Assert.Equal(shortName, environment.EnvironmentShort);
    }

    [Fact]
    public void GetByTag_WithInvalidTag_Throws()
    {
        Assert.Throws<ArgumentException>(() => AppEnvironments.GetByTag("unknown"));
    }

    [Fact]
    public void IsValidHelpers_RespectCaseInsensitivity()
    {
        Assert.True(AppEnvironments.IsValidAppTag("PROD"));
        Assert.True(AppEnvironments.IsValidEnvironment("production"));
        Assert.False(AppEnvironments.IsValidAppTag("devops"));
    }
}
