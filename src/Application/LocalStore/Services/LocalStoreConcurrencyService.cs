using System.Collections.Concurrent;

namespace Application.LocalStore.Services;

internal class LocalStoreConcurrencyService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphoreSlims = new();

    public async Task<IDisposable> AcquireAsync(string group, CancellationToken cancellationToken = default)
    {
        // Get or create a semaphore for this group
        var semaphore = _semaphoreSlims.GetOrAdd(group, _ => new SemaphoreSlim(1, 1));
        
        // Wait for the semaphore
        await semaphore.WaitAsync(cancellationToken);
        
        // Return a disposable that releases the semaphore when disposed
        return new ConcurrencyTicket(semaphore, group, _semaphoreSlims);
    }

    private class ConcurrencyTicket : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly string _group;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphoreSlims;
        private bool _disposed = false;

        public ConcurrencyTicket(SemaphoreSlim semaphore, string group, ConcurrentDictionary<string, SemaphoreSlim> semaphoreSlims)
        {
            _semaphore = semaphore;
            _group = group;
            _semaphoreSlims = semaphoreSlims;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _semaphore.Release();
                
                // Optionally clean up the semaphore if no one is waiting
                // This prevents memory leaks for groups that are no longer used
                if (_semaphore.CurrentCount > 0)
                {
                    _semaphoreSlims.TryRemove(_group, out _);
                }
                
                _disposed = true;
            }
        }
    }
}