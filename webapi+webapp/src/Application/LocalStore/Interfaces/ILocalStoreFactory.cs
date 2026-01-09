using System.Threading;
using System.Threading.Tasks;
using Application.LocalStore.Services;

namespace Application.LocalStore.Interfaces;

public interface ILocalStoreFactory
{
    Task<ConcurrentLocalStore> OpenStore(string group = "common_group", CancellationToken cancellationToken = default);
}
