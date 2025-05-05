using AbsolutePathHelpers;
using Application.Logger.Extensions;
using Application.NativeCmd.Exceptions;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;

namespace Application.NativeCmd.Services;

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
        using var _ = logger.BeginScopeMap<CmdService>(serviceAction: nameof(BuildRun), scopeMap: new Dictionary<string, object?>()
        {
            ["CmdPath"] = path,
            ["CmdArgs"] = string.Join(" ", args),
            ["WorkingDirectory"] = workingDirectory,
            ["EnvironmentVariables"] = environmentVariables?.ToDictionary()
        });

        logger.Trace("Building command {CmdPath} {CmdArgs}", path, string.Join(" ", args));

        Command osCli = Cli.Wrap(path)
            .WithArguments(args, false)
            .WithValidation(CommandResultValidation.None);

        if (workingDirectory != null)
        {
            osCli = osCli
                .WithWorkingDirectory(workingDirectory);
        }

        if (environmentVariables != null)
        {
            osCli = osCli
            .WithEnvironmentVariables(environmentVariables.ToDictionary());
        }

        if (inPipeTarget != null)
        {
            osCli = osCli
                .WithStandardInputPipe(inPipeTarget);
        }

        if (outPipeTarget != null)
        {
            osCli = osCli
                .WithStandardOutputPipe(outPipeTarget);
        }

        if (errPipeTarget != null)
        {
            osCli = osCli
                .WithStandardErrorPipe(errPipeTarget);
        }

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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return BuildRun("cmd", ["/c", $"\"{command}\""], workingDirectory, environmentVariables, inPipeTarget, outPipeTarget, errPipeTarget);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return BuildRun("/bin/bash", ["-c", $"\"{command}\""], workingDirectory, environmentVariables, inPipeTarget, outPipeTarget, errPipeTarget);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public async Task<string> RunOnce(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        var stdBuffer = new StringBuilder();

        var result = await BuildRun(path, args, workingDirectory, environmentVariables, null, PipeTarget.ToStringBuilder(stdBuffer), PipeTarget.ToStringBuilder(stdBuffer))
            .ExecuteAsync(stoppingToken);

        if (result.ExitCode != 0)
        {
            throw new NativeCmdException(stdBuffer.ToString(), result.ExitCode);
        }

        return stdBuffer.ToString();
    }

    public async Task<string> RunOnce(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        var stdBuffer = new StringBuilder();

        var result = await BuildRun(command, workingDirectory, environmentVariables, null, PipeTarget.ToStringBuilder(stdBuffer), PipeTarget.ToStringBuilder(stdBuffer))
            .ExecuteAsync(stoppingToken);

        if (result.ExitCode != 0)
        {
            throw new NativeCmdException(stdBuffer.ToString(), result.ExitCode);
        }

        return stdBuffer.ToString();
    }

    public async Task<string> RunOnceAndIgnore(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        var stdBuffer = new StringBuilder();

        _ = await BuildRun(command, workingDirectory, environmentVariables, null, PipeTarget.ToStringBuilder(stdBuffer), PipeTarget.ToStringBuilder(stdBuffer))
            .ExecuteAsync(stoppingToken);

        return stdBuffer.ToString();
    }

    public IAsyncEnumerable<CommandEvent> RunListen(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        PipeSource? inPipeTarget = default,
        CancellationToken stoppingToken = default)
    {
        var osCli = BuildRun(path, args, workingDirectory, environmentVariables, inPipeTarget);

        return osCli.ListenAsync(stoppingToken);
    }

    public async Task RunListenAndLog(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        PipeSource? inPipeTarget = default,
        CancellationToken stoppingToken = default)
    {
        using var _ = logger.BeginScopeMap<CmdService>(serviceAction: nameof(RunListenAndLog), scopeMap: new Dictionary<string, object?>()
        {
            ["CmdPath"] = path,
            ["CmdArgs"] = string.Join(" ", args),
            ["WorkingDirectory"] = workingDirectory,
            ["EnvironmentVariables"] = environmentVariables?.ToDictionary()
        });

        await foreach (var cmdEvent in RunListen(path, args, workingDirectory, environmentVariables, inPipeTarget, stoppingToken))
        {
            switch (cmdEvent)
            {
                case StandardOutputCommandEvent stdOut:
                    logger.LogTrace("{x}", stdOut.Text);
                    break;
                case StandardErrorCommandEvent stdErr:
                    logger.LogTrace("{x}", stdErr.Text);
                    break;
                case ExitedCommandEvent exited:
                    var msg = $"{path} ended with return code {exited.ExitCode}";
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
        var osCli = BuildRun(command, workingDirectory, environmentVariables, inPipeTarget);

        return osCli.ListenAsync(stoppingToken);
    }

    public async Task RunListenAndLog(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        PipeSource? inPipeTarget = default,
        CancellationToken stoppingToken = default)
    {
        using var _ = logger.BeginScopeMap<CmdService>(serviceAction: nameof(RunListenAndLog), scopeMap: new Dictionary<string, object?>()
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
