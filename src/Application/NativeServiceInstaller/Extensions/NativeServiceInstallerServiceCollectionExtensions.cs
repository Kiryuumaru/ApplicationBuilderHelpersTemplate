using Application.NativeServiceInstaller.Interfaces;
using Application.NativeServiceInstaller.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;

namespace Application.NativeServiceInstaller.Extensions;

internal static class NativeServiceInstallerServiceCollectionExtensions
{
    public static IServiceCollection AddNativeServiceInstallerServices(this IServiceCollection services)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            services.AddScoped<INativeServiceInstaller, WindowsServiceInstallerService>();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            services.AddScoped<INativeServiceInstaller, LinuxServiceInstallerService>();
        else
            throw new PlatformNotSupportedException("Unsupported platform for NativeServiceInstaller.");

        services.AddScoped<NativeServiceInstallerService>();

        return services;
    }
}
