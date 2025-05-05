using Application.LocalStore.Features;
using Application.LocalStore.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.LocalStore.Services;

public class LocalStoreFactory(IServiceProvider serviceProvider)
{
    public async Task<ConcurrentLocalStore> OpenStore(string group = "common_group", CancellationToken cancellationToken = default)
    {
        var localStoreConcurrencyService = serviceProvider.GetRequiredService<LocalStoreConcurrencyService>();
        var ticket = await localStoreConcurrencyService.Aquire(group, cancellationToken);
        var localStoreService = serviceProvider.GetRequiredService<ILocalStoreService>();
        var localStore = new ConcurrentLocalStore(localStoreService)
        {
            Group = group
        };
        localStore.CancelWhenDisposing().Register(ticket.Dispose);
        return localStore;
    }
}
