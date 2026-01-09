using System;
using System.Threading;
using System.Threading.Tasks;
using Application.LocalStore.Services;
using Microsoft.Extensions.DependencyInjection;
using Application.LocalStore.Interfaces;
using Application.LocalStore.Common;

namespace Application.LocalStore.Services;

internal sealed class LocalStoreFactory(IServiceProvider serviceProvider) : ILocalStoreFactory
{
    public async Task<ConcurrentLocalStore> OpenStore(string group = "common_group", CancellationToken cancellationToken = default)
    {
        var normalizedGroup = LocalStoreKey.NormalizeGroup(group);

        var localStoreService = serviceProvider.GetRequiredService<ILocalStoreService>();

        await localStoreService.Open(cancellationToken);

        return new ConcurrentLocalStore(localStoreService, normalizedGroup);
    }
}