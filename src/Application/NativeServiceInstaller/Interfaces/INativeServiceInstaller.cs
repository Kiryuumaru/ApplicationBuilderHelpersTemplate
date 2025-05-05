using AbsolutePathHelpers;
using Application.NativeServiceInstaller.Enums;

namespace Application.NativeServiceInstaller.Interfaces;

public interface INativeServiceInstaller
{
    Task Install(string serviceName, string serviceDescription, AbsolutePath executablePath, string[] executableArgs, AbsolutePath workingDirectory, Dictionary<string, string?> environmentVariables, CancellationToken cancellationToken = default);

    Task Start(string serviceName, CancellationToken cancellationToken = default);

    Task Stop(string serviceName, CancellationToken cancellationToken = default);

    Task Uninstall(string serviceName, CancellationToken cancellationToken = default);

    Task<NativeServiceStatus> GetStatus(string serviceName, CancellationToken cancellationToken = default);
}
