using Application.LocalStore.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.LocalStore.Extensions;

internal static class LocalStoreServiceCollectionExtensions
{
    public static IServiceCollection AddLocalStoreServices(this IServiceCollection services)
    {
        services.AddSingleton<LocalStoreConcurrencyService>();
        services.AddTransient<LocalStoreFactory>();
        return services;
    }
}
