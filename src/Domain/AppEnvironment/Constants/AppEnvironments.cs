namespace Domain.AppEnvironment.Constants;

public static partial class AppEnvironments
{
    public static partial Models.AppEnvironment[] AllValues { get; }

    public static bool IsValidAppTag(string appTag)
    {
        return AllValues.Any(x => x.Tag.Equals(appTag, StringComparison.InvariantCultureIgnoreCase));
    }

    public static bool IsValidEnvironment(string environment)
    {
        return AllValues.Any(x => x.Environment.Equals(environment, StringComparison.InvariantCultureIgnoreCase));
    }

    public static Models.AppEnvironment GetByTag(string tag)
    {
        return AllValues.FirstOrDefault(x => x.Tag.Equals(tag, StringComparison.InvariantCultureIgnoreCase))
            ?? throw new ArgumentException($"Invalid environment tag: {tag}");
    }

    public static Models.AppEnvironment GetByEnvironment(string environment)
    {
        return AllValues.FirstOrDefault(x => x.Environment.Equals(environment, StringComparison.InvariantCultureIgnoreCase))
            ?? throw new ArgumentException($"Invalid environment: {environment}");
    }
}
