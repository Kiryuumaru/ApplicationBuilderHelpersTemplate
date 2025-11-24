namespace Domain.AppEnvironment.Constants;

/// <summary>
/// Provides predefined application environment descriptors for the Jack Of All Trades trading platform.
/// </summary>
/// <remarks>
/// <para>
/// The structure mirrors our release cadence:
/// - <strong>Alpha (Development)</strong>: daily development experiments and local testing
/// - <strong>Beta (Staging)</strong>: integration testing against shared services
/// - <strong>Rc (Preproduction)</strong>: release-candidate validation before production cutovers
/// - <strong>Prod (Production)</strong>: customer-facing, capital-at-risk workloads
/// </para>
/// <para>
/// Each entry supplies the tag, the friendly environment name, and the short identifier used for build metadata, telemetry, and deployment automation.
/// </para>
/// <para>
/// Basic helpers exist to validate tags/environments and to retrieve a descriptor by either property.
/// </para>
/// </remarks>
public static class AppEnvironments
{
    /// <summary>
    /// Development environment configuration with development API endpoints and MQTT broker.
    /// </summary>
    /// <remarks>
    /// Used for development and testing. Provides access to development-specific services
    /// and allows unrestricted testing without affecting production systems.
    /// </remarks>
    public static Models.AppEnvironment Alpha { get; } = new Models.AppEnvironment
    {
        Environment = "Development",
        EnvironmentShort = "dev",
        Tag = "alpha"
    };

    /// <summary>
    /// Staging environment configuration with staging API endpoints and MQTT broker.
    /// </summary>
    /// <remarks>
    /// Used for integration testing and staging deployments. Provides a production-like
    /// environment for testing complete workflows before production deployment.
    /// </remarks>
    public static Models.AppEnvironment Beta { get; } = new Models.AppEnvironment
    {
        Environment = "Staging",
        EnvironmentShort = "stg",
        Tag = "beta"
    };

    /// <summary>
    /// Pre-production environment configuration with pre-production API endpoints and MQTT broker.
    /// </summary>
    /// <remarks>
    /// Used for final validation before production deployment. Provides a production-identical
    /// environment for final testing and validation of releases.
    /// </remarks>
    public static Models.AppEnvironment Rc { get; } = new Models.AppEnvironment
    {
        Environment = "Preproduction",
        EnvironmentShort = "pre",
        Tag = "rc"
    };

    /// <summary>
    /// Production environment configuration with production API endpoints and MQTT broker.
    /// </summary>
    /// <remarks>
    /// Used for live production deployments. Provides access to production services
    /// and should only be used for validated, stable releases.
    /// </remarks>
    public static Models.AppEnvironment Prod { get; } = new Models.AppEnvironment
    {
        Environment = "Production",
        EnvironmentShort = "prod",
        Tag = "prod"
    };

    /// <summary>
    /// Array containing all available application environment configurations.
    /// </summary>
    /// <remarks>
    /// Ordered by deployment progression: Development, Staging, Preproduction, Production.
    /// Used for validation, enumeration, and environment discovery operations.
    /// </remarks>
    public static Models.AppEnvironment[] AllValues { get; } =
    [
        Alpha,
        Beta,
        Rc,
        Prod
    ];

    /// <summary>
    /// Validates whether the specified application tag is supported.
    /// </summary>
    /// <param name="appTag">The application tag to validate (alpha, beta, rc, prod).</param>
    /// <returns>True if the tag is valid and supported, false otherwise.</returns>
    /// <remarks>
    /// Performs case-insensitive comparison against all defined environment tags.
    /// Valid tags: "alpha", "beta", "rc", "prod".
    /// </remarks>
    public static bool IsValidAppTag(string appTag)
    {
        return AllValues.Any(x => x.Tag.Equals(appTag, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    /// Validates whether the specified environment name is supported.
    /// </summary>
    /// <param name="environment">The environment name to validate (Development, Staging, Preproduction, Production).</param>
    /// <returns>True if the environment name is valid and supported, false otherwise.</returns>
    /// <remarks>
    /// Performs case-insensitive comparison against all defined environment names.
    /// Valid environments: "Development", "Staging", "Preproduction", "Production".
    /// </remarks>
    public static bool IsValidEnvironment(string environment)
    {
        return AllValues.Any(x => x.Environment.Equals(environment, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    /// Retrieves the application environment configuration by tag.
    /// </summary>
    /// <param name="tag">The environment tag to look up (alpha, beta, rc, prod).</param>
    /// <returns>The application environment configuration for the specified tag.</returns>
    /// <exception cref="ArgumentException">Thrown when the tag is not recognized.</exception>
    /// <remarks>
    /// Performs case-insensitive lookup. Valid tags are: "alpha", "beta", "rc", "prod".
    /// Use this method when you have an environment tag and need the full configuration.
    /// </remarks>
    public static Models.AppEnvironment GetByTag(string tag)
    {
        return AllValues.FirstOrDefault(x => x.Tag.Equals(tag, StringComparison.InvariantCultureIgnoreCase))
            ?? throw new ArgumentException($"Invalid environment tag: {tag}");
    }

    /// <summary>
    /// Retrieves the application environment configuration by environment name.
    /// </summary>
    /// <param name="environment">The environment name to look up (Development, Staging, Preproduction, Production).</param>
    /// <returns>The application environment configuration for the specified environment.</returns>
    /// <exception cref="ArgumentException">Thrown when the environment name is not recognized.</exception>
    /// <remarks>
    /// Performs case-insensitive lookup. Valid environments are: "Development", "Staging", "Preproduction", "Production".
    /// Use this method when you have an environment name and need the full configuration.
    /// </remarks>
    public static Models.AppEnvironment GetByEnvironment(string environment)
    {
        return AllValues.FirstOrDefault(x => x.Environment.Equals(environment, StringComparison.InvariantCultureIgnoreCase))
            ?? throw new ArgumentException($"Invalid environment: {environment}");
    }
}
