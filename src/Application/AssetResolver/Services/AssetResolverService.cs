using AbsolutePathHelpers;
using Application.Common.Extensions;
using Application.Configuration.Extensions;
using Application.Logger.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.AssetResolver.Services;

public class AssetResolverService(ILogger<AssetResolverService> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
{
    public async Task<AbsolutePath> GetAssetAsync(string assetFilename, Uri remoteUrl, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScopeMap<AssetResolverService>(scopeMap: new Dictionary<string, object?>
        {
            { "AssetFilename", assetFilename },
            { "RemoteUrl", remoteUrl }
        });

        logger.Debug("Resolving asset {AssetFilename}", assetFilename);

        var assetFilePath = configuration.GetAssetsPath() / assetFilename;

        if (assetFilePath.FileExists())
        {
            logger.Debug("Asset {AssetFilename} already exists", assetFilename, assetFilePath);
            return assetFilePath;
        }

        var assetDownloadFilePath = configuration.GetTempPath() / $"dl-{RandomHelpers.Alphanumeric(20)}";
        await assetDownloadFilePath.Delete(cancellationToken);

        logger.Info("Asset {AssetFilename} does not exist, downloading from {RemoteUrl}", assetFilename, remoteUrl);

        using var httpClient = httpClientFactory.CreateClient();

        await httpClient.DownloadFile(remoteUrl, assetDownloadFilePath,
            progress =>
            {
                using var _ = logger.BeginScopeMap<AssetResolverService>(scopeMap: new Dictionary<string, object?>
                {
                    { "AssetFilename", assetFilename },
                    { "RemoteUrl", remoteUrl },
                    { "AssetFilePath", assetFilePath },
                    { "AssetDownloadFilePath", assetDownloadFilePath },
                    { "BytesDownloaded", progress.BytesDownloaded },
                    { "TotalBytesDownloaded", progress.TotalBytesDownloaded },
                    { "TotalBytes", progress.TotalBytes },
                    { "Percentage", progress.Percentage }
                });

                if (progress.Percentage.HasValue && progress.TotalBytes.HasValue)
                {
                    logger.Info("Downloading {AssetFilename}: {TotalBytesDownloaded}/{TotalBytes} bytes ({Percentage:F2}%)");
                }
                else
                {
                    logger.Info("Downloading {AssetFilename}: {TotalBytesDownloaded} total bytes downloaded.");
                }
            }, TimeSpan.FromSeconds(1), cancellationToken: cancellationToken);

        await assetFilePath.Delete(cancellationToken);
        await assetDownloadFilePath.CopyTo(assetFilePath, cancellationToken);
        await assetDownloadFilePath.Delete(cancellationToken);

        logger.Info("Asset {AssetFilename} downloaded", assetFilename);

        return assetFilePath;
    }
}
