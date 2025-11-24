using System.Diagnostics;

namespace Application.Common.Extensions;

/// <summary>
/// Provides extension methods for the Stopwatch class to calculate elapsed time in different units.
/// Offers high-precision timing measurements for performance monitoring and benchmarking in the
/// Viana Edge Grid system.
/// </summary>
/// <remarks>
/// <para>
/// These extension methods provide convenient access to elapsed time in various units without
/// the need for manual conversion calculations. They use the high-resolution Stopwatch.Frequency
/// for accurate timing measurements.
/// </para>
/// <para>
/// The methods are particularly useful for:
/// - Performance monitoring and profiling
/// - Benchmarking operations
/// - Detailed timing analysis
/// - Microsecond and nanosecond precision measurements
/// </para>
/// <para>
/// All calculations are based on Stopwatch.ElapsedTicks and Stopwatch.Frequency for maximum
/// accuracy and consistency across different platforms and hardware configurations.
/// </para>
/// </remarks>
public static class StopwatchExtensions
{
    /// <summary>
    /// Gets the elapsed time in nanoseconds as a double-precision floating-point number.
    /// </summary>
    /// <param name="watch">The Stopwatch instance to measure elapsed time for.</param>
    /// <returns>The elapsed time in nanoseconds with high precision.</returns>
    /// <remarks>
    /// <para>
    /// Provides nanosecond-level precision for extremely detailed timing measurements.
    /// Useful for measuring very short operations or when high-precision timing is required.
    /// </para>
    /// <para>
    /// The calculation uses: ElapsedTicks * 1,000,000,000 / Stopwatch.Frequency
    /// This provides the most precise timing measurement available from the Stopwatch class.
    /// </para>
    /// </remarks>
    public static double ElapsedNanoSeconds(this Stopwatch watch)
    {
        return watch.ElapsedTicks * 1000000000 / (double)Stopwatch.Frequency;
    }

    /// <summary>
    /// Gets the elapsed time in microseconds as a double-precision floating-point number.
    /// </summary>
    /// <param name="watch">The Stopwatch instance to measure elapsed time for.</param>
    /// <returns>The elapsed time in microseconds with high precision.</returns>
    /// <remarks>
    /// <para>
    /// Provides microsecond-level precision suitable for measuring short operations and
    /// performance-critical code sections. More practical than nanoseconds for most timing scenarios.
    /// </para>
    /// <para>
    /// The calculation uses: ElapsedTicks * 1,000,000 / Stopwatch.Frequency
    /// This provides excellent precision for performance monitoring and optimization.
    /// </para>
    /// </remarks>
    public static double ElapsedMicroSeconds(this Stopwatch watch)
    {
        return watch.ElapsedTicks * 1000000 / (double)Stopwatch.Frequency;
    }

    /// <summary>
    /// Gets the elapsed time in milliseconds as a double-precision floating-point number.
    /// </summary>
    /// <param name="watch">The Stopwatch instance to measure elapsed time for.</param>
    /// <returns>The elapsed time in milliseconds with high precision.</returns>
    /// <remarks>
    /// <para>
    /// Provides an alternative to Stopwatch.ElapsedMilliseconds with double precision instead
    /// of long integer precision. Useful when fractional milliseconds matter for timing analysis.
    /// </para>
    /// <para>
    /// The calculation uses: ElapsedTicks * 1,000 / Stopwatch.Frequency
    /// This provides higher precision than the built-in ElapsedMilliseconds property which returns a long.
    /// </para>
    /// </remarks>
    public static double ElapsedMilliSeconds(this Stopwatch watch)
    {
        return watch.ElapsedTicks * 1000 / (double)Stopwatch.Frequency;
    }
}
