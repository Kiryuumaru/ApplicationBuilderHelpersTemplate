using Application.Abstractions.Application;
using Application.AppEnvironment.Extensions;
using Application.LocalStore.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Application.AppEnvironment.Services;

public class AppEnvironmentService(IConfiguration configuration, ILocalStoreFactory localStoreFactory, IApplicationConstants applicationConstants)
{
    public async Task<Domain.AppEnvironment.Models.AppEnvironment> GetEnvironment(CancellationToken cancellationToken = default)
    {
        string? appTag = null;
        try
        {
            appTag = configuration.GetAppTagOverride();
        }
        catch { }
        if (string.IsNullOrEmpty(appTag))
        {
            try
            {
                using var store = await localStoreFactory.OpenStore(cancellationToken: cancellationToken);
                var storedValue = await store.Get("VIANA_EDGE_GRID_APP_TAG", cancellationToken);
                if (!string.IsNullOrWhiteSpace(storedValue))
                {
                    appTag = storedValue;
                }
            }
            catch
            {
                // Database might not be initialized yet during startup
            }
        }
        if (string.IsNullOrEmpty(appTag))
        {
            appTag = applicationConstants.AppTag;
        }
        return Domain.AppEnvironment.Constants.AppEnvironments.GetByTag(appTag);
    }
}
