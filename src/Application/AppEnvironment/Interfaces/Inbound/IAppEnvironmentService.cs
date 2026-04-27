namespace Application.AppEnvironment.Interfaces.Inbound;

public interface IAppEnvironmentService
{
    Task<Domain.AppEnvironment.Models.AppEnvironment> GetEnvironment(CancellationToken cancellationToken = default);
}
