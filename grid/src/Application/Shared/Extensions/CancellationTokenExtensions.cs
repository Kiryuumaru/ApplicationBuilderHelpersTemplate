using System.Runtime.CompilerServices;

namespace Application.Shared.Extensions;

/// <summary>
/// Extension methods for CancellationToken.
/// </summary>
public static class CancellationTokenExtensions
{
    private static readonly ConditionalWeakTable<object, TokenSourceTracker> _trackers = [];

    private sealed class TokenSourceTracker(CancellationTokenSource timeoutSource, CancellationTokenSource linkedSource)
    {
        private readonly CancellationTokenSource _timeoutSource = timeoutSource;
        private readonly CancellationTokenSource _linkedSource = linkedSource;
        private CancellationTokenRegistration _registration;
        private int _disposed;

        public void SetRegistration(CancellationTokenRegistration registration)
        {
            _registration = registration;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try { _registration.Unregister(); } catch { }
                try { _timeoutSource?.Dispose(); } catch { }
                try { _linkedSource?.Dispose(); } catch { }
            }
        }

        ~TokenSourceTracker() => Dispose();
    }

    /// <summary>
    /// Creates a linked cancellation token that will be cancelled when either the original token 
    /// is cancelled or the specified timeout elapses.
    /// </summary>
    public static CancellationToken WithTimeout(this CancellationToken cancellationToken, TimeSpan timeout)
    {
        var timeoutCts = new CancellationTokenSource(timeout);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var resultToken = linkedCts.Token;

        var tracker = new TokenSourceTracker(timeoutCts, linkedCts);
        var trackingKey = new object();
        _trackers.Add(trackingKey, tracker);

        try
        {
            var registration = resultToken.Register(state =>
            {
                var key = state!;
                if (_trackers.TryGetValue(key, out var trackerToDispose))
                {
                    _trackers.Remove(key);
                    trackerToDispose.Dispose();
                }
            }, trackingKey);

            tracker.SetRegistration(registration);
        }
        catch (ObjectDisposedException)
        {
            _trackers.Remove(trackingKey);
            tracker.Dispose();
            throw;
        }

        return resultToken;
    }

    /// <summary>
    /// Returns a task that completes when the cancellation token is cancelled.
    /// </summary>
    public static Task WhenCanceled(this CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        cancellationToken.Register(() => tcs.TrySetResult(true));
        return tcs.Task;
    }
}
