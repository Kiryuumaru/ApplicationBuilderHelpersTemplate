using AbsolutePathHelpers;
using Application.AssetResolver.Services;
using Application.Configuration.Extensions;
using Application.Logger.Extensions;
using Application.NativeServiceInstaller.Enums;
using Application.NativeServiceInstaller.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.NativeServiceInstaller.Services;

public class NativeServiceInstallerService(
    ILogger<NativeServiceInstallerService> logger,
    IConfiguration configuration,
    INativeServiceInstaller nativeServiceInstaller,
    AssetResolverService assetResolverService)
{
    public async Task Install(
        string serviceName,
        string serviceDescription,
        string serviceVersion,
        string fileExtension,
        Uri remoteUri,
        Func<(AbsolutePath AssetFilePath, AbsolutePath ExtractDirectory, CancellationToken CancellationToken), Task> extractCallback,
        Func<(AbsolutePath ExtractDirectory, CancellationToken CancellationToken), Task<AbsolutePath>> executablePathFactory,
        Func<(AbsolutePath ExtractDirectory, CancellationToken CancellationToken), Task<AbsolutePath>> workingDirectoryFactory,
        string[] executableArgs,
        Dictionary<string, string?> environmentVariables,
        CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<NativeServiceInstallerService>(nameof(Install), scopeMap: new Dictionary<string, object?>
        {
            ["NativeServiceName"] = serviceName,
            ["NativeServiceDescription"] = serviceDescription,
            ["NativeExecutableArgs"] = executableArgs,
            ["NativeEnvironmentVariables"] = environmentVariables
        });

        var assetDownloadFilename = $"{serviceName}-{serviceVersion}.{fileExtension.Trim('.')}";

        logger.LogInformation("Starting installation of service {NativeServiceName}", serviceName);

        var filePath = await assetResolverService.GetAssetAsync(assetDownloadFilename, remoteUri, cancellationToken: cancellationToken);
        filePath.Parent?.CreateDirectory();
        var releaseDir = configuration.GetReleasesPath() / serviceName.ToLowerInvariant() / serviceVersion.ToLowerInvariant();
        var executablePath = await executablePathFactory((releaseDir, cancellationToken));
        var workingDirectory = await workingDirectoryFactory((releaseDir, cancellationToken));
        await releaseDir.CreateOrCleanDirectory(cancellationToken: cancellationToken);
        await extractCallback((filePath, releaseDir, cancellationToken));

        try
        {
            logger.LogDebug("Attempting to stop service {NativeServiceName} if running", serviceName);
            await nativeServiceInstaller.Stop(serviceName, cancellationToken);
            logger.LogInformation("Successfully stopped service {NativeServiceName}", serviceName);
        }
        catch { }

        try
        {
            logger.LogDebug("Attempting to uninstall service {NativeServiceName} if installed", serviceName);
            await nativeServiceInstaller.Uninstall(serviceName, cancellationToken);
            logger.LogInformation("Successfully uninstalled service {NativeServiceName}", serviceName);
        }
        catch { }

        try
        {
            logger.LogDebug("Attempting to install service {NativeServiceName}", serviceName);
            await nativeServiceInstaller.Install(serviceName, serviceDescription, executablePath, executableArgs, workingDirectory, environmentVariables, cancellationToken);
            logger.LogInformation("Successfully installed service {NativeServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install service {NativeServiceName}: {ErrorMessage}", serviceName, ex.Message);
            throw;
        }
    }

    public async Task Install(
        string serviceName,
        string serviceDescription,
        string serviceVersion,
        string fileExtension,
        Uri remoteUri,
        Func<(AbsolutePath ExtractDirectory, CancellationToken CancellationToken), Task<AbsolutePath>> executablePathFactory,
        Func<(AbsolutePath ExtractDirectory, CancellationToken CancellationToken), Task<AbsolutePath>> workingDirectoryFactory,
        string[] executableArgs,
        Dictionary<string, string?> environmentVariables,
        CancellationToken cancellationToken = default)
    {
        var workingDirectory = configuration.GetReleasesPath() / serviceName.ToLowerInvariant() / serviceVersion.ToLowerInvariant();
        await Install(serviceName, serviceDescription, serviceVersion, fileExtension, remoteUri, async extract =>
        {
            if (fileExtension.EndsWith("tar.gz") ||
                fileExtension.EndsWith("tar") ||
                fileExtension.EndsWith("zip"))
            {
                await extract.AssetFilePath.UncompressTo(extract.ExtractDirectory, extract.CancellationToken);
            }
            else
            {
                await extract.AssetFilePath.CopyTo(extract.ExtractDirectory / extract.AssetFilePath.Name, extract.CancellationToken);
            }

        }, executablePathFactory, workingDirectoryFactory, executableArgs, environmentVariables, cancellationToken);
    }

    public async Task Start(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<NativeServiceInstallerService>(nameof(Start), scopeMap: new Dictionary<string, object?>
        {
            ["NativeServiceName"] = serviceName
        });

        logger.LogInformation("Starting service {NativeServiceName}", serviceName);

        try
        {
            logger.LogDebug("Attempting to stop service {NativeServiceName} before starting", serviceName);
            await nativeServiceInstaller.Stop(serviceName, cancellationToken);
            logger.LogInformation("Successfully stopped service {NativeServiceName}", serviceName);
        }
        catch { }

        try
        {
            logger.LogDebug("Attempting to start service {NativeServiceName}", serviceName);
            await nativeServiceInstaller.Start(serviceName, cancellationToken);
            logger.LogInformation("Successfully started service {NativeServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start service {NativeServiceName}: {ErrorMessage}", serviceName, ex.Message);
            throw;
        }
    }

    public async Task Stop(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<NativeServiceInstallerService>(nameof(Stop), scopeMap: new Dictionary<string, object?>
        {
            ["NativeServiceName"] = serviceName
        });

        logger.LogInformation("Stopping service {NativeServiceName}", serviceName);

        try
        {
            await nativeServiceInstaller.Stop(serviceName, cancellationToken);
            logger.LogInformation("Successfully stopped service {NativeServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop service {NativeServiceName}", serviceName);
            throw;
        }
    }

    public async Task Uninstall(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<NativeServiceInstallerService>(nameof(Uninstall), scopeMap: new Dictionary<string, object?>
        {
            ["NativeServiceName"] = serviceName
        });

        logger.LogInformation("Uninstalling service {NativeServiceName}", serviceName);

        try
        {
            logger.LogDebug("Attempting to stop service {NativeServiceName} before uninstalling", serviceName);
            await nativeServiceInstaller.Stop(serviceName, cancellationToken);
            logger.LogInformation("Successfully stopped service {NativeServiceName}", serviceName);
        }
        catch { }

        try
        {
            logger.LogDebug("Attempting to uninstall service {NativeServiceName}", serviceName);
            await nativeServiceInstaller.Uninstall(serviceName, cancellationToken);
            logger.LogInformation("Successfully uninstalled service {NativeServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to uninstall service {NativeServiceName}", serviceName);
            throw;
        }
    }

    public async Task<NativeServiceStatus> GetStatus(string serviceName, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<NativeServiceInstallerService>(nameof(GetStatus), scopeMap: new Dictionary<string, object?>
        {
            ["NativeServiceName"] = serviceName
        });

        logger.LogInformation("Retrieving status for service {NativeServiceName}", serviceName);

        try
        {
            var status = await nativeServiceInstaller.GetStatus(serviceName, cancellationToken);
            logger.LogInformation("Service {NativeServiceName} is currently {NativeServiceStatus}", serviceName, status);
            return status;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve status for service {NativeServiceName}: {ErrorMessage}", serviceName, ex.Message);
            throw;
        }
    }
}
