using Presentation.WebApp.Client.Components.Theme.Extensions;
using Presentation.WebApp.Client.Components.Theme.Interfaces;
using Presentation.WebApp.Client.Components.Theme.Services;
using Presentation.WebApp.Client.Notifications.Extensions;

namespace Presentation.WebApp.Client.Components.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClientComponents(this IServiceCollection services)
    {
        services.AddThemeServices();
        services.AddNotificationServices();

        return services;
    }
}
