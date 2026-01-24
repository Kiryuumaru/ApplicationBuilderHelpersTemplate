namespace Application.Shared.Extensions;

/// <summary>
/// Provides extension methods for the TimeSpan struct to create cancellation tokens and sources.
/// Simplifies the creation of time-based cancellation mechanisms for async operations
/// </summary>
/// <remarks>
/// <para>
/// These extension methods provide convenient shortcuts for creating cancellation tokens from
/// TimeSpan values, which is a common pattern in async programming for implementing timeouts.
/// </para>
/// <para>
/// The methods are particularly useful for:
/// - Setting operation timeouts
/// - Creating time-based cancellation for async operations
/// - Implementing deadline-based processing
/// - Timeout management in service calls and network operations
/// </para>
/// <para>
/// These extensions follow the fluent API pattern, allowing for readable and concise code
/// when working with time-based cancellation scenarios.
/// </para>
/// </remarks>
public static class TimeSpanExtensions
{
    /// <summary>
    /// Creates a CancellationTokenSource that will be cancelled after the specified TimeSpan duration.
    /// </summary>
    /// <param name="value">The TimeSpan duration after which the cancellation should occur.</param>
    /// <returns>A new CancellationTokenSource configured to cancel after the specified duration.</returns>
    /// <remarks>
    /// <para>
    /// This method creates a new CancellationTokenSource with an automatic timeout based on the
    /// provided TimeSpan. The source will automatically trigger cancellation when the specified
    /// time elapses.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var timeout = TimeSpan.FromSeconds(30);
    /// using var cts = timeout.ToCancellationTokenSource();
    /// await SomeAsyncOperation(cts.Token);
    /// </code>
    /// </para>
    /// <para>
    /// The caller is responsible for disposing the returned CancellationTokenSource to avoid
    /// resource leaks, especially in long-running applications.
    /// </para>
    /// </remarks>
    public static CancellationTokenSource ToCancellationTokenSource(this TimeSpan value)
    {
        return new(value);
    }

    /// <summary>
    /// Creates a CancellationToken that will be cancelled after the specified TimeSpan duration.
    /// </summary>
    /// <param name="value">The TimeSpan duration after which the cancellation should occur.</param>
    /// <returns>A CancellationToken that will be cancelled after the specified duration.</returns>
    /// <remarks>
    /// <para>
    /// This method is a convenience wrapper that creates a CancellationTokenSource with the
    /// specified timeout and returns its Token property. This is useful when you only need
    /// the token and don't need direct access to the source.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var timeout = TimeSpan.FromMinutes(5);
    /// await SomeAsyncOperation(timeout.ToCancellationToken());
    /// </code>
    /// </para>
    /// <para>
    /// Note: This method creates a CancellationTokenSource internally, which may lead to resource
    /// usage. For scenarios where you need more control over resource management, consider using
    /// ToCancellationTokenSource() and managing the source lifetime explicitly.
    /// </para>
    /// </remarks>
    public static CancellationToken ToCancellationToken(this TimeSpan value)
    {
        return ToCancellationTokenSource(value).Token;
    }
}
