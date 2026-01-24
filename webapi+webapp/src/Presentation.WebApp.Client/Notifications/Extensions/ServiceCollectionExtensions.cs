using Presentation.WebApp.Client.Notifications.Interfaces;
using Presentation.WebApp.Client.Notifications.Services;

namespace Presentation.WebApp.Client.Notifications.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        services.AddSingleton<ToastService>();
        services.AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>());
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());

        return services;
    }
}
