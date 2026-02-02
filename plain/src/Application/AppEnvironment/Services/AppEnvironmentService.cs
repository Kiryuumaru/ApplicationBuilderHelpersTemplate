using Application.AppEnvironment.Extensions;
using Application.AppEnvironment.Interfaces;
using Application.Shared.Interfaces.Inbound;
using Microsoft.Extensions.Configuration;

namespace Application.AppEnvironment.Services;

internal sealed class AppEnvironmentService(
    IConfiguration configuration,
    IApplicationConstants applicationConstants) : IAppEnvironmentService
{
    public Task<Domain.AppEnvironment.Models.AppEnvironment> GetEnvironment(CancellationToken cancellationToken = default)
    {
        string? appTag = configuration.GetAppTagOverride();
        if (string.IsNullOrEmpty(appTag))
        {
            appTag = applicationConstants.AppTag;
        }
        return Task.FromResult(Domain.AppEnvironment.Constants.AppEnvironments.GetByTag(appTag));
    }
}
