namespace Presentation.WebApp.FunctionalTests;

/// <summary>
/// A Fact attribute with a default 5-minute timeout.
/// Use this instead of [Fact] for tests that should fail if they run longer than 5 minutes.
/// </summary>
public class TimedFactAttribute : FactAttribute
{
    /// <summary>
    /// Default timeout of 5 minutes in milliseconds.
    /// </summary>
    public const int DefaultTimeoutMs = 300_000;

    public TimedFactAttribute()
    {
        Timeout = DefaultTimeoutMs;
    }
}

/// <summary>
/// A Theory attribute with a default 5-minute timeout.
/// Use this instead of [Theory] for parameterized tests that should fail if they run longer than 5 minutes.
/// </summary>
public class TimedTheoryAttribute : TheoryAttribute
{
    /// <summary>
    /// Default timeout of 5 minutes in milliseconds.
    /// </summary>
    public const int DefaultTimeoutMs = 300_000;

    public TimedTheoryAttribute()
    {
        Timeout = DefaultTimeoutMs;
    }
}
