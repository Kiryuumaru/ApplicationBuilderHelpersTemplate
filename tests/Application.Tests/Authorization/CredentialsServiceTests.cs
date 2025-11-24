using Application.Authorization.Services;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Nodes;

namespace Application.Tests.Authorization;

public class CredentialsServiceTests
{
	[Fact]
	public async Task GetCredentials_ReadsEnvironmentSpecificValues()
	{
		var configuration = BuildConfiguration(new JsonObject
		{
			["dev"] = new JsonObject
			{
				["jwt"] = new JsonObject
				{
					["secret"] = "dev-secret",
					["issuer"] = "https://issuer.dev",
					["audience"] = "https://audience.dev",
					["default_expiration_seconds"] = 7200,
					["clock_skew_seconds"] = 45
				}
			}
		});

		var service = new CredentialsService(appEnvironmentService: null!, configuration);

		var credentials = await service.GetCredentials("dev", CancellationToken.None);

		Assert.Equal("dev-secret", credentials.JwtConfiguration.Secret);
		Assert.Equal("https://issuer.dev", credentials.JwtConfiguration.Issuer);
		Assert.Equal("https://audience.dev", credentials.JwtConfiguration.Audience);
		Assert.Equal(TimeSpan.FromHours(2), credentials.JwtConfiguration.DefaultExpiration);
		Assert.Equal(TimeSpan.FromSeconds(45), credentials.JwtConfiguration.ClockSkew);
	}

	[Fact]
	public async Task GetCredentials_UsesDefaultsWhenOptionalValuesMissing()
	{
		var configuration = BuildConfiguration(new JsonObject
		{
			["prod"] = new JsonObject
			{
				["jwt"] = new JsonObject
				{
					["secret"] = "prod-secret",
					["issuer"] = "https://issuer.prod",
					["audience"] = "https://audience.prod"
				}
			}
		});

		var service = new CredentialsService(appEnvironmentService: null!, configuration);

		var credentials = await service.GetCredentials("prod", CancellationToken.None);

		Assert.Equal(TimeSpan.FromHours(1), credentials.JwtConfiguration.DefaultExpiration);
		Assert.Equal(TimeSpan.FromMinutes(5), credentials.JwtConfiguration.ClockSkew);
	}

	[Fact]
	public async Task GetCredentials_ClampsNegativeDurationsToZero()
	{
		var configuration = BuildConfiguration(new JsonObject
		{
			["test"] = new JsonObject
			{
				["jwt"] = new JsonObject
				{
					["secret"] = "test-secret",
					["issuer"] = "https://issuer.test",
					["audience"] = "https://audience.test",
					["default_expiration_seconds"] = -30,
					["clock_skew_seconds"] = -10
				}
			}
		});

		var service = new CredentialsService(appEnvironmentService: null!, configuration);

		var credentials = await service.GetCredentials("test", CancellationToken.None);

		Assert.Equal(TimeSpan.Zero, credentials.JwtConfiguration.DefaultExpiration);
		Assert.Equal(TimeSpan.Zero, credentials.JwtConfiguration.ClockSkew);
	}

	private static IConfiguration BuildConfiguration(JsonObject credentials)
	{
		var builder = new ConfigurationBuilder();
		builder.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["VEG_RUNTIME_CREDENTIALS"] = credentials.ToJsonString()
		});
		return builder.Build();
	}
}
