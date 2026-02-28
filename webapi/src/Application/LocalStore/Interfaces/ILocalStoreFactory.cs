namespace Application.LocalStore.Interfaces;

public interface ILocalStoreFactory
{
    Task<IConcurrentLocalStore> OpenStore(string group = "common_group", CancellationToken cancellationToken = default);
}
