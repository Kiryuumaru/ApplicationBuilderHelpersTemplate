namespace Application.Common.Extensions;

public static class CancellationTokenExtensions
{
    public static CancellationToken WithTimeout(this CancellationToken cancellationToken, TimeSpan timeout)
    {
        if (cancellationToken.IsCancellationRequested)
            return cancellationToken;

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        return combinedCts.Token;
    }

    public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, combinedCts.Token));
        
        if (completedTask == task)
        {
            return await task;
        }
        else
        {
            throw new TimeoutException($"Operation timed out after {timeout}");
        }
    }

    public static async Task WithTimeout(this Task task, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, combinedCts.Token));
        
        if (completedTask == task)
        {
            await task;
        }
        else
        {
            throw new TimeoutException($"Operation timed out after {timeout}");
        }
    }
}