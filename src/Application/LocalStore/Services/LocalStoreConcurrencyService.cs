using DisposableHelpers;
using System.Collections.Concurrent;

namespace Application.LocalStore.Services;

public class LocalStoreConcurrencyService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> semaphoreSlims = new();

    public async Task<IDisposable> Aquire(string group, CancellationToken cancellationToken)
    {
        SemaphoreSlim semaphore;
        lock (semaphoreSlims)
        {
            semaphore = semaphoreSlims.GetOrAdd(group, _ => new SemaphoreSlim(1));
        }
        await semaphore.WaitAsync(cancellationToken);
        return new Disposable(disposing =>
        {
            if (disposing)
            {
                semaphore.Release();
                lock (semaphoreSlims)
                {
                    semaphoreSlims.Remove(group, out _);
                }
            }
        });
    }
}
