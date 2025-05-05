using AbsolutePathHelpers;
using Application.Logger.Extensions;
using Application.NativeCmd.Services;
using Application.NativeServiceInstaller.Enums;
using Application.NativeServiceInstaller.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Application.NativeServiceInstaller.Services;

internal class LinuxServiceInstallerService(ILogger<LinuxServiceInstallerService> logger, CmdService cmdService) : INativeServiceInstaller
{
    public async Task Install(string serviceName, string serviceDescription, AbsolutePath executablePath, string[] executableArgs, AbsolutePath workingDirectory, Dictionary<string, string?> environmentVariables, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<LinuxServiceInstallerService>(serviceAction: nameof(Install), scopeMap: new Dictionary<string, object?>()
        {
            ["SystemdServiceName"] = serviceName,
            ["SystemdServiceDescription"] = serviceDescription,
            ["SystemdExecutablePath"] = executablePath,
            ["SystemdExecutableArgs"] = string.Join(" ", executableArgs),
            ["SystemdWorkingDirectory"] = workingDirectory,
            ["SystemdEnvironmentVariables"] = environmentVariables
        });

        logger.LogInformation("Installing Linux service {SystemdServiceName}", serviceName);

        AbsolutePath serviceFilePath = AbsolutePath.Create($"/etc/systemd/system/{serviceName}.service");
        StringBuilder serviceFileContent = new();

        serviceFileContent.AppendLine("[Unit]");
        serviceFileContent.AppendLine($"Description={serviceDescription}");
        serviceFileContent.AppendLine("After=multi-user.target");

        serviceFileContent.AppendLine("[Service]");
        serviceFileContent.AppendLine($"ExecStart={executablePath} {string.Join(" ", executableArgs)}");
        serviceFileContent.AppendLine($"WorkingDirectory={workingDirectory}");
        serviceFileContent.AppendLine("Restart=always");
        foreach (var envVar in environmentVariables)
        {
            serviceFileContent.AppendLine($"Environment={envVar.Key}={envVar.Value}");
        }

        serviceFileContent.AppendLine("[Install]");
        serviceFileContent.AppendLine("WantedBy=multi-user.target");

        await serviceFilePath.WriteAllText(serviceFileContent.ToString(), cancellationToken);

        await cmdService.RunListenAndLog($"chmod +x {serviceFilePath}", stoppingToken: cancellationToken);
        await cmdService.RunListenAndLog($"systemctl enable {serviceName}", stoppingToken: cancellationToken);
        await cmdService.RunListenAndLog($"systemctl daemon-reload", stoppingToken: cancellationToken);

        logger.LogInformation("Linux service {SystemdServiceName} installed successfully", serviceName);
    }

    public async Task Start(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<LinuxServiceInstallerService>(serviceAction: nameof(Start), scopeMap: new Dictionary<string, object?>()
        {
            ["SystemdServiceName"] = serviceName
        });

        logger.LogInformation("Starting Linux service {SystemdServiceName}", serviceName);

        await cmdService.RunListenAndLog($"systemctl start {serviceName}", stoppingToken: cancellationToken);

        logger.LogInformation("Linux service {SystemdServiceName} started successfully", serviceName);
    }

    public async Task Stop(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<LinuxServiceInstallerService>(serviceAction: nameof(Stop), scopeMap: new Dictionary<string, object?>()
        {
            ["SystemdServiceName"] = serviceName
        });

        logger.LogInformation("Stopping Linux service {SystemdServiceName}", serviceName);

        await cmdService.RunListenAndLog($"systemctl stop {serviceName}", stoppingToken: cancellationToken);

        logger.LogInformation("Linux service {SystemdServiceName} stopped successfully", serviceName);
    }

    public async Task Uninstall(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<LinuxServiceInstallerService>(serviceAction: nameof(Uninstall), scopeMap: new Dictionary<string, object?>()
        {
            ["SystemdServiceName"] = serviceName
        });

        logger.LogInformation("Uninstalling Linux service {SystemdServiceName}", serviceName);

        await Stop(serviceName, cancellationToken);
        await cmdService.RunListenAndLog("systemctl disable {serviceName}", stoppingToken: cancellationToken);
        AbsolutePath serviceFilePath = AbsolutePath.Create($"/etc/systemd/system/{serviceName}.service");
        if (serviceFilePath.FileExists())
        {
            await serviceFilePath.Delete(cancellationToken);
        }
        await cmdService.RunListenAndLog($"systemctl daemon-reload", stoppingToken: cancellationToken);

        logger.LogInformation("Linux service {SystemdServiceName} uninstalled successfully", serviceName);
    }

    public async Task<NativeServiceStatus> GetStatus(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<LinuxServiceInstallerService>(serviceAction: nameof(GetStatus), scopeMap: new Dictionary<string, object?>()
        {
            ["SystemdServiceName"] = serviceName
        });

        logger.LogInformation("Retrieving status for Linux service {SystemdServiceName}", serviceName);

        try
        {
            string result = await cmdService.RunOnce($"systemctl is-active {serviceName}", stoppingToken: cancellationToken);

            return result.Trim().ToLowerInvariant() switch
            {
                "active" => NativeServiceStatus.Running,
                "inactive" => NativeServiceStatus.Stopped,
                _ => NativeServiceStatus.NotInstalled
            };
        }
        catch
        {
            return NativeServiceStatus.NotInstalled;
        }
    }
}
