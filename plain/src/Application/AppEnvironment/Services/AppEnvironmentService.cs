using Application.AppEnvironment.Extensions;
using Application.Shared.Interfaces.Inbound;
using Microsoft.Extensions.Configuration;

namespace Application.AppEnvironment.Services;

public class AppEnvironmentService(
    IConfiguration configuration,
    IApplicationConstants applicationConstants)
{
    public async Task<Domain.AppEnvironment.Models.AppEnvironment> GetEnvironment(CancellationToken cancellationToken = default)
    {
        string? appTag = configuration.GetAppTagOverride();
        if (string.IsNullOrEmpty(appTag))
        {
            appTag = applicationConstants.AppTag;
        }
        return Domain.AppEnvironment.Constants.AppEnvironments.GetByTag(appTag);
    }
}
