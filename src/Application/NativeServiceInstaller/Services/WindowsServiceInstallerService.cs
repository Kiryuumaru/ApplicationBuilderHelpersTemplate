using AbsolutePathHelpers;
using Application.AssetResolver.Services;
using Application.Configuration.Extensions;
using Application.Logger.Extensions;
using Application.NativeCmd.Services;
using Application.NativeServiceInstaller.Enums;
using Application.NativeServiceInstaller.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.NativeServiceInstaller.Services;

internal class WindowsServiceInstallerService(ILogger<WindowsServiceInstallerService> logger, NativeCmdService cmdService, IConfiguration configuration, AssetResolverService assetResolverService) : INativeServiceInstaller
{
    public async Task Install(string serviceName, string serviceDescription, AbsolutePath executablePath, string[] executableArgs, AbsolutePath workingDirectory, Dictionary<string, string?> environmentVariables, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<WindowsServiceInstallerService>(serviceAction: nameof(Install), scopeMap: new Dictionary<string, object?>()
        {
            ["WinswServiceName"] = serviceName,
            ["WinswServiceDescription"] = serviceDescription,
            ["WinswExecutablePath"] = executablePath,
            ["WinswExecutableArgs"] = string.Join(" ", executableArgs),
            ["WinswWorkingDirectory"] = workingDirectory,
            ["WinswEnvironmentVariables"] = environmentVariables
        });

        logger.LogDebug("Installing Windows service {WinswServiceName}", serviceName);

        var configPath = configuration.GetServicesPath() / serviceName.ToLowerInvariant() / $"{serviceName.ToLowerInvariant()}.xml";

        var winswExec = await PrepareServiceWrapper(cancellationToken);

        var configContent =
            $"<service>\n" +
            $"  <id>{serviceName}</id>\n" +
            $"  <name>{serviceName}</name>\n" +
            $"  <description>{serviceDescription}</description>\n" +
            $"  <executable>{executablePath}</executable>\n" +
            $"  <arguments>{string.Join(" ", executableArgs)}</arguments>\n" +
            $"  <workingdirectory>{workingDirectory}</workingdirectory>\n" +
            $"  <log mode=\"roll\"></log>\n" +
            $"  <startmode>Automatic</startmode>\n" +
            $"  <onfailure action=\"restart\" delay=\"2 sec\"/>\n" +
            $"  <outfilepattern>.output.log</outfilepattern>\n" +
            $"  <errfilepattern>.error.log</errfilepattern>\n" +
            $"  <combinedfilepattern>.combined.log</combinedfilepattern>\n" +
            $"{string.Join(Environment.NewLine, environmentVariables.Select(kv => $"  <env name=\"{kv.Key}\" value=\"{kv.Value}\" />"))}\n" +
            $"</service>";

        await configPath.WriteAllText(configContent, cancellationToken);

        await cmdService.RunListenAndLog($"\"{winswExec}\" install \"{configPath}\"", stoppingToken: cancellationToken);

        logger.LogDebug("Windows service {WinswServiceName} installed successfully", serviceName);
    }

    public async Task Start(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<WindowsServiceInstallerService>(serviceAction: nameof(Start), scopeMap: new Dictionary<string, object?>()
        {
            ["WinswServiceName"] = serviceName
        });

        logger.LogDebug("Starting Windows service {WinswServiceName}", serviceName);

        var winswExec = await PrepareServiceWrapper(cancellationToken);
        var configPath = configuration.GetServicesPath() / serviceName.ToLowerInvariant() / $"{serviceName.ToLowerInvariant()}.xml";

        if (!configPath.FileExists())
        {
            throw new InvalidOperationException($"Service config file not found: {configPath}");
        }
        await cmdService.RunListenAndLog($"\"{winswExec}\" start \"{configPath}\"", stoppingToken: cancellationToken);
        logger.LogDebug("Windows service {WinswServiceName} started successfully", serviceName);
    }

    public async Task Stop(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<WindowsServiceInstallerService>(serviceAction: nameof(Stop), scopeMap: new Dictionary<string, object?>()
        {
            ["WinswServiceName"] = serviceName
        });

        logger.LogDebug("Stopping Windows service {WinswServiceName}", serviceName);

        var winswExec = await PrepareServiceWrapper(cancellationToken);
        var configPath = configuration.GetServicesPath() / serviceName.ToLowerInvariant() / $"{serviceName.ToLowerInvariant()}.xml";

        if (!configPath.FileExists())
        {
            throw new InvalidOperationException($"Service config file not found: {configPath}");
        }
        await cmdService.RunListenAndLog($"\"{winswExec}\" stop \"{configPath}\"", stoppingToken: cancellationToken);
        logger.LogDebug("Windows service {WinswServiceName} stopped successfully", serviceName);
    }

    public async Task Uninstall(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<WindowsServiceInstallerService>(serviceAction: nameof(Uninstall), scopeMap: new Dictionary<string, object?>()
        {
            ["WinswServiceName"] = serviceName
        });

        logger.LogDebug("Uninstalling Windows service {WinswServiceName}", serviceName);

        var winswExec = await PrepareServiceWrapper(cancellationToken);
        var configPath = configuration.GetServicesPath() / serviceName.ToLowerInvariant() / $"{serviceName.ToLowerInvariant()}.xml";

        if (!configPath.FileExists())
        {
            throw new InvalidOperationException($"Service config file not found: {configPath}");
        }
        await cmdService.RunListenAndLog($"\"{winswExec}\" uninstall \"{configPath}\"", stoppingToken: cancellationToken);
        logger.LogDebug("Windows service {WinswServiceName} uninstalled successfully", serviceName);
    }

    public async Task<NativeServiceStatus> GetStatus(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<WindowsServiceInstallerService>(serviceAction: nameof(GetStatus), scopeMap: new Dictionary<string, object?>()
        {
            ["WinswServiceName"] = serviceName
        });

        logger.LogDebug("Retrieving status for Windows service {WinswServiceName}", serviceName);

        var winswExec = await PrepareServiceWrapper(cancellationToken);
        var configPath = configuration.GetServicesPath() / serviceName.ToLowerInvariant() / $"{serviceName.ToLowerInvariant()}.xml";

        if (!configPath.FileExists())
        {
            return NativeServiceStatus.NotInstalled;
        }

        try
        {
            var output = await cmdService.RunOnceAndIgnore($"\"{winswExec}\" status \"{configPath}\"", stoppingToken: cancellationToken);

            if (output.Contains("Active", StringComparison.InvariantCultureIgnoreCase))
            {
                return NativeServiceStatus.Running;
            }
            else if (output.Contains("Inactive", StringComparison.InvariantCultureIgnoreCase))
            {
                return NativeServiceStatus.Stopped;
            }
            else
            {
                return NativeServiceStatus.NotInstalled;
            }
        }
        catch
        {
            return NativeServiceStatus.NotInstalled;
        }
    }

    private async Task<AbsolutePath> PrepareServiceWrapper(CancellationToken cancellationToken)
    {
        using var _ = logger.BeginScopeMap(nameof(WindowsServiceInstallerService), nameof(PrepareServiceWrapper));

        return await assetResolverService.GetAssetAsync("WinSW-x64-v3.0.0-alpha.11.exe", new Uri($"https://public.viana.ai/installer/WinSW-x64-v3.0.0-alpha.11.exe"), cancellationToken);
    }
}
