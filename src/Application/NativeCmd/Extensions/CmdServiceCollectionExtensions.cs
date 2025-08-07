using Application.NativeCmd.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.NativeCmd.Extensions;

internal static class CmdServiceCollectionExtensions
{
    public static IServiceCollection AddCmdServices(this IServiceCollection services)
    {
        services.AddScoped<NativeCmdService>();
        return services;
    }
}
