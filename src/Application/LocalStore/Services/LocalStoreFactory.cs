using Application.LocalStore.Features;
using Application.LocalStore.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.LocalStore.Services;

public class LocalStoreFactory(IServiceProvider serviceProvider)
{
    public async Task<ConcurrentLocalStore> OpenStore(string group = "common_group", CancellationToken cancellationToken = default)
    {
        var concurrencyService = serviceProvider.GetRequiredService<LocalStoreConcurrencyService>();
        var localStoreService = serviceProvider.GetRequiredService<ILocalStoreService>();
        
        // Acquire the concurrency lock for this group
        var concurrencyTicket = await concurrencyService.AcquireAsync(group, cancellationToken);
        
        var localStore = new ConcurrentLocalStore(localStoreService, concurrencyTicket)
        {
            Group = group
        };
        
        return localStore;
    }
}
