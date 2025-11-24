using AbsolutePathHelpers;
using Application.Common.Extensions;
using Application.Logger.Extensions;
using Application.NativeCmd.Exceptions;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Application.NativeCmd.Services;

#pragma warning disable CA1822 // Mark members as static
public class CmdService(ILogger<CmdService> logger)
{
    public Command BuildRun(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        PipeSource? inPipeTarget = default,
        PipeTarget? outPipeTarget = default,
        PipeTarget? errPipeTarget = default)
    {
        using var _ = logger.BeginScopeMap(scopeMap: new Dictionary<string, object?>()
        {
            ["CmdPath"] = path,
            ["CmdArgs"] = string.Join(" ", args),
            ["WorkingDirectory"] = workingDirectory,
            ["EnvironmentVariables"] = environmentVariables?.ToDictionary()
        });

        logger.Trace("Building command {CmdPath} {CmdArgs}", path, string.Join(" ", args));

        Command osCli = CliHelpers.BuildRun(path, args, workingDirectory, environmentVariables, inPipeTarget, outPipeTarget, errPipeTarget);

        logger.Trace("Command {CmdPath} {CmdArgs} built", path, string.Join(" ", args));

        return osCli;
    }

    public Command BuildRun(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        PipeSource? inPipeTarget = default,
        PipeTarget? outPipeTarget = default,
        PipeTarget? errPipeTarget = default)
    {
        return CliHelpers.BuildRun(command, workingDirectory, environmentVariables, inPipeTarget, outPipeTarget, errPipeTarget);
    }

    public Task<string> RunOnce(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        return CliHelpers.RunOnce(path, args, workingDirectory, environmentVariables, stoppingToken);
    }

    public Task<string> RunOnce(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        return CliHelpers.RunOnce(command, workingDirectory, environmentVariables, stoppingToken);
    }

    public Task<string> RunOnceAndIgnore(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        return CliHelpers.RunOnceAndIgnore(command, workingDirectory, environmentVariables, stoppingToken);
    }

    public IAsyncEnumerable<CommandEvent> RunListen(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        PipeSource? inPipeTarget = default,
        CancellationToken stoppingToken = default)
    {
        return CliHelpers.RunListen(path, args, workingDirectory, environmentVariables, inPipeTarget, stoppingToken);
    }

    public async Task RunListenAndLog(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        PipeSource? inPipeTarget = default,
        CancellationToken stoppingToken = default)
    {
        using var _ = logger.BeginScopeMap(scopeMap: new Dictionary<string, object?>()
        {
            ["CmdPath"] = path,
            ["CmdArgs"] = string.Join(" ", args),
            ["WorkingDirectory"] = workingDirectory,
            ["EnvironmentVariables"] = environmentVariables?.ToDictionary()
        });

        string errors = "";

        await foreach (var cmdEvent in RunListen(path, args, workingDirectory, environmentVariables, inPipeTarget, stoppingToken))
        {
            switch (cmdEvent)
            {
                case StandardOutputCommandEvent stdOut:
                    logger.LogTrace("{x}", stdOut.Text);
                    break;
                case StandardErrorCommandEvent stdErr:
                    logger.LogTrace("{x}", stdErr.Text);
                    if (errors != "")
                    {
                        errors += "\n";
                    }
                    errors += stdErr.Text;
                    break;
                case ExitedCommandEvent exited:
                    var msg = $"{path} ended with return code {exited.ExitCode}: " + errors;
                    if (exited.ExitCode != 0)
                    {
                        throw new NativeCmdException(msg, exited.ExitCode);
                    }
                    else
                    {
                        logger.LogTrace("{x}", msg);
                    }
                    break;
            }
        }
    }

    public IAsyncEnumerable<CommandEvent> RunListen(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        PipeSource? inPipeTarget = default,
        CancellationToken stoppingToken = default)
    {
        return CliHelpers.RunListen(command, workingDirectory, environmentVariables, inPipeTarget, stoppingToken);
    }

    public async Task RunListenAndLog(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        PipeSource? inPipeTarget = default,
        CancellationToken stoppingToken = default)
    {
        using var _ = logger.BeginScopeMap(scopeMap: new Dictionary<string, object?>()
        {
            ["CmdCommand"] = command,
            ["WorkingDirectory"] = workingDirectory,
            ["EnvironmentVariables"] = environmentVariables?.ToDictionary()
        });

        string errors = "";

        await foreach (var cmdEvent in RunListen(command, workingDirectory, environmentVariables, inPipeTarget, stoppingToken))
        {
            switch (cmdEvent)
            {
                case StandardOutputCommandEvent stdOut:
                    logger.LogTrace("{x}", stdOut.Text);
                    break;
                case StandardErrorCommandEvent stdErr:
                    logger.LogTrace("{x}", stdErr.Text);
                    if (errors != "")
                    {
                        errors += "\n";
                    }
                    errors += stdErr.Text;
                    break;
                case ExitedCommandEvent exited:
                    var msg = $"{command} ended with return code {exited.ExitCode}: " + errors;
                    if (exited.ExitCode != 0)
                    {
                        throw new NativeCmdException(msg, exited.ExitCode);
                    }
                    else
                    {
                        logger.LogTrace("{x}", msg);
                    }
                    break;
            }
        }
    }
}

#pragma warning restore CA1822 // Mark members as static