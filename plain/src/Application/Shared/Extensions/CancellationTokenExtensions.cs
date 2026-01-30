namespace Application.Shared.Extensions;

/// <summary>
/// Extension methods for CancellationToken.
/// </summary>
public static class CancellationTokenExtensions
{
    /// <summary>
    /// Returns a task that completes when the cancellation token is cancelled.
    /// </summary>
    public static Task WhenCanceled(this CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        CancellationTokenRegistration? reg = null;
        reg = cancellationToken.Register(s =>
        {
            ((TaskCompletionSource<bool>)s!).TrySetResult(true);
            reg?.Unregister();
        }, tcs);
        return tcs.Task;
    }
}
