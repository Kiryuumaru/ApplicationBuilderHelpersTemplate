using Application.Common.Features;
using DisposableHelpers;

namespace Application.Common.Extensions;

public static class TaskUtils
{
    public static async Task DelayAndForget(int milliseconds, CancellationToken cancellationToken = default)
    {
        await DelayAndForget(TimeSpan.FromMilliseconds(milliseconds), cancellationToken);
    }

    public static async Task DelayAndForget(TimeSpan timeSpan, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(timeSpan, cancellationToken);
        }
        catch { }
    }

    public static void Forget(this Task task)
    {
        if (!task.IsCompleted || task.IsFaulted)
        {
            _ = ForgetAwaited(task);
        }

        async static Task ForgetAwaited(Task task)
        {
            try
            {
                await task;
            }
            catch { }
        }
    }

    public static Task WaitThread(this Task task)
    {
        return ThreadHelpers.WaitThread(() => task);
    }

    /// <summary>
    /// Retries an async action with configurable delay and retry count.
    /// </summary>
    /// <typeparam name="T">The return type of the action.</typeparam>
    /// <param name="action">The action to retry.</param>
    /// <param name="onRetry">Optional callback executed on each retry attempt.</param>
    /// <param name="retryDelay">Delay between retry attempts. Default is 2 seconds.</param>
    /// <param name="maxRetries">Maximum number of retries. Use -1 for unlimited retries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the action.</returns>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> action,
        Action<(Exception Error, int Attempts)>? onRetry = null,
        TimeSpan? retryDelay = default,
        int maxRetries = -1,
        CancellationToken cancellationToken = default)
    {
        return await RetryInternalAsync(action, onRetry, retryDelay, maxRetries, cancellationToken);
    }

    /// <summary>
    /// Retries an async action with configurable delay and retry count.
    /// </summary>
    /// <typeparam name="T">The return type of the action.</typeparam>
    /// <param name="action">The action to retry.</param>
    /// <param name="onRetry">Optional async callback executed on each retry attempt.</param>
    /// <param name="retryDelay">Delay between retry attempts. Default is 2 seconds.</param>
    /// <param name="maxRetries">Maximum number of retries. Use -1 for unlimited retries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the action.</returns>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> action,
        Func<(Exception Error, int Attempts), Task>? onRetry = null,
        TimeSpan? retryDelay = default,
        int maxRetries = -1,
        CancellationToken cancellationToken = default)
    {
        return await RetryInternalAsync(action, onRetry, retryDelay, maxRetries, cancellationToken);
    }

    /// <summary>
    /// Retries an async action with configurable delay and retry count.
    /// </summary>
    /// <param name="action">The action to retry.</param>
    /// <param name="onRetry">Optional callback executed on each retry attempt.</param>
    /// <param name="retryDelay">Delay between retry attempts. Default is 2 seconds.</param>
    /// <param name="maxRetries">Maximum number of retries. Use -1 for unlimited retries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task RetryAsync(
        Func<Task> action,
        Action<(Exception Error, int Attempts)>? onRetry = null,
        TimeSpan? retryDelay = default,
        int maxRetries = -1,
        CancellationToken cancellationToken = default)
    {
        await RetryAsync<object?>(async () => { await action(); return null; }, onRetry, retryDelay, maxRetries, cancellationToken);
    }

    public static Disposable DisposableTask(
        Func<CancellationToken, Task> task,
        Func<Exception, bool>? onDisposeError = null,
        Func<Exception, bool>? onTaskError = null)
    {
        CancellationTokenSource cts = new();
        Task? backgroundTask = null;

        Disposable disposable = new(async disposing =>
        {
            if (disposing)
            {
                cts.Cancel();

                // Wait for the background task to complete (with timeout)
                if (backgroundTask != null)
                {
                    try
                    {
                        await backgroundTask.WaitAsync(TimeSpan.FromMinutes(1));
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            onDisposeError?.Invoke(ex);
                        }
                        catch
                        {
                            // Ignore exceptions from error handler
                        }
                    }
                }

                try
                {
                    cts.Dispose();
                }
                catch { }
                try
                {
                    backgroundTask?.Dispose();
                }
                catch { }
            }
        });

        backgroundTask = Task.Run(async () =>
        {
            try
            {
                await task(cts.Token);
            }
            catch (Exception ex)
            {
                try
                {
                    onTaskError?.Invoke(ex);
                }
                catch
                {
                    // Ignore exceptions from error handler
                }
            }
        });

        return disposable;
    }

    private static async Task<T> RetryInternalAsync<T>(
        Func<Task<T>> action,
        object? onRetry,
        TimeSpan? retryDelay,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        retryDelay ??= TimeSpan.FromSeconds(2);
        int attempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await action();
            }
            catch (Exception ex) when (maxRetries < 0 || attempt < maxRetries)
            {
                attempt++;

                // Handle both sync and async onRetry callbacks
                switch (onRetry)
                {
                    case Action<(Exception, int)> syncCallback:
                        syncCallback((ex, attempt));
                        break;
                    case Func<(Exception, int), Task> asyncCallback:
                        await asyncCallback((ex, attempt));
                        break;
                }

                await Task.Delay(retryDelay.Value, cancellationToken);
            }
        }
    }
}
