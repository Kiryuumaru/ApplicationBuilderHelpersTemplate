namespace Infrastructure.EFCore.Interfaces;

public interface IDatabaseInitializationState
{
    bool IsInitialized { get; }
    Task WaitForInitializationAsync(CancellationToken cancellationToken = default);
}
