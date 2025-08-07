using Application.LocalStore.Features;
using Application.LocalStore.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.LocalStore.Services;

public class LocalStoreFactory(IServiceProvider serviceProvider)
{
    public async Task<ConcurrentLocalStore> OpenStore(string group = "common_group", CancellationToken cancellationToken = default)
    {
        // Create a new transient instance of the local store service
        var localStoreService = serviceProvider.GetRequiredService<ILocalStoreService>();
        
        // Open the transaction-scoped service
        await localStoreService.Open(cancellationToken);
        
        // Create the local store with the opened service
        var localStore = new ConcurrentLocalStore(localStoreService)
        {
            Group = group
        };
        
        return localStore;
    }
}
