using AbsolutePathHelpers;
using Application.Common.Extensions;
using Application.Logger.Extensions;
using Application.NativeCmd.Exceptions;
using Application.NativeCmd.Extensions;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.NativeCmd.Services;

public class CmdService(ILogger<CmdService> logger, IConfiguration configuration)
{
    private readonly ILogger<CmdService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public Command BuildRun(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        PipeSource? inPipeTarget = default,
        PipeTarget? outPipeTarget = default,
        PipeTarget? errPipeTarget = default)
    {
        using var _ = _logger.BeginScopeMap(scopeMap: new Dictionary<string, object?>()
        {
            ["CmdPath"] = path,
            ["CmdArgs"] = string.Join(" ", args),
            ["WorkingDirectory"] = workingDirectory,
            ["EnvironmentVariables"] = environmentVariables?.ToDictionary()
        });

        _logger.Trace("Building command {CmdPath} {CmdArgs}", path, string.Join(" ", args));

        Command osCli = CliHelpers.BuildRun(path, args, workingDirectory, environmentVariables, inPipeTarget, outPipeTarget, errPipeTarget);

        _logger.Trace("Command {CmdPath} {CmdArgs} built", path, string.Join(" ", args));

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
        _logger.Trace("Building command {CmdCommand}", command);
        return CliHelpers.BuildRun(command, workingDirectory, environmentVariables, inPipeTarget, outPipeTarget, errPipeTarget);
    }

    public Task<string> RunOnce(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        _logger.Trace("Running once {CmdPath} {CmdArgs}", path, string.Join(" ", args));
        return CliHelpers.RunOnce(path, args, workingDirectory, environmentVariables, stoppingToken);
    }

    public Task<string> RunOnce(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        _logger.Trace("Running once {CmdCommand}", command);
        return CliHelpers.RunOnce(command, workingDirectory, environmentVariables, stoppingToken);
    }

    public Task<string> RunOnceAndIgnore(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        _logger.Trace("Running once (ignore errors) {CmdCommand}", command);
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
        _logger.Trace("Running listen {CmdPath} {CmdArgs}", path, string.Join(" ", args));
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
        using var _ = _logger.BeginScopeMap(scopeMap: new Dictionary<string, object?>()
        {
            ["CmdPath"] = path,
            ["CmdArgs"] = string.Join(" ", args),
            ["WorkingDirectory"] = workingDirectory,
            ["EnvironmentVariables"] = environmentVariables?.ToDictionary()
        });

        string errors = "";

        bool isVerbose = _configuration.GetIsVerboseCliLogger();

        await foreach (var cmdEvent in RunListen(path, args, workingDirectory, environmentVariables, inPipeTarget, stoppingToken))
        {
            switch (cmdEvent)
            {
                case StandardOutputCommandEvent stdOut:
                    if (isVerbose)
                        _logger.LogTrace("{x}", stdOut.Text);
                    else
                        _logger.LogDebug("{x}", stdOut.Text);
                    break;
                case StandardErrorCommandEvent stdErr:
                    if (isVerbose)
                        _logger.LogTrace("{x}", stdErr.Text);
                    else
                        _logger.LogDebug("{x}", stdErr.Text);
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
                        if (isVerbose)
                            _logger.LogTrace("{x}", msg);
                        else
                            _logger.LogDebug("{x}", msg);
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
        _logger.Trace("Running listen {CmdCommand}", command);
        return CliHelpers.RunListen(command, workingDirectory, environmentVariables, inPipeTarget, stoppingToken);
    }

    public async Task RunListenAndLog(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        PipeSource? inPipeTarget = default,
        CancellationToken stoppingToken = default)
    {
        using var _ = _logger.BeginScopeMap(scopeMap: new Dictionary<string, object?>()
        {
            ["CmdCommand"] = command,
            ["WorkingDirectory"] = workingDirectory,
            ["EnvironmentVariables"] = environmentVariables?.ToDictionary()
        });

        string errors = "";

        bool isVerbose = _configuration.GetIsVerboseCliLogger();

        await foreach (var cmdEvent in RunListen(command, workingDirectory, environmentVariables, inPipeTarget, stoppingToken))
        {
            switch (cmdEvent)
            {
                case StandardOutputCommandEvent stdOut:
                    if (isVerbose)
                        _logger.LogTrace("{x}", stdOut.Text);
                    else
                        _logger.LogDebug("{x}", stdOut.Text);
                    break;
                case StandardErrorCommandEvent stdErr:
                    if (isVerbose)
                        _logger.LogTrace("{x}", stdErr.Text);
                    else
                        _logger.LogDebug("{x}", stdErr.Text);
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
                        if (isVerbose)
                            _logger.LogTrace("{x}", msg);
                        else
                            _logger.LogDebug("{x}", msg);
                    }
                    break;
            }
        }
    }
}