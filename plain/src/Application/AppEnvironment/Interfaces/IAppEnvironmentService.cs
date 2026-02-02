namespace Application.AppEnvironment.Interfaces;

public interface IAppEnvironmentService
{
    Task<Domain.AppEnvironment.Models.AppEnvironment> GetEnvironment(CancellationToken cancellationToken = default);
}
