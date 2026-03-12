namespace Infrastructure.EFCore.Interfaces;

/// <summary>
/// Provider-specific database bootstrap for EF Core setup operations.
/// Internal to Infrastructure layer - not exposed to Application or Presentation.
/// </summary>
public interface IEFCoreDatabaseBootstrap
{
    Task SetupAsync(CancellationToken cancellationToken = default);
}
