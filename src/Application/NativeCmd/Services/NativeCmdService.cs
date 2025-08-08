using AbsolutePathHelpers;
using Application.Logger.Extensions;
using Application.NativeCmd.Exceptions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace Application.NativeCmd.Services;

public class NativeCmdService(ILogger<NativeCmdService> logger)
{
    public ProcessStartInfo BuildRun(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        bool redirectStandardInput = false,
        bool redirectStandardOutput = false,
        bool redirectStandardError = false)
    {
        using var _ = logger.BeginScopeMap<NativeCmdService>(serviceAction: nameof(BuildRun), scopeMap: new Dictionary<string, object?>()
        {
            ["CmdPath"] = path,
            ["CmdArgs"] = string.Join(" ", args),
            ["WorkingDirectory"] = workingDirectory,
            ["EnvironmentVariables"] = environmentVariables?.ToDictionary()
        });

        logger.Trace("Building command {CmdPath} {CmdArgs}", path, string.Join(" ", args));

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = redirectStandardOutput,
            RedirectStandardError = redirectStandardError
        };

        // Add arguments
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (workingDirectory != null)
        {
            startInfo.WorkingDirectory = workingDirectory.ToString();
        }

        if (environmentVariables != null)
        {
            foreach (var envVar in environmentVariables)
            {
                if (envVar.Value != null)
                {
                    startInfo.Environment[envVar.Key] = envVar.Value;
                }
            }
        }

        logger.Trace("Command {CmdPath} {CmdArgs} built", path, string.Join(" ", args));

        return startInfo;
    }

    public ProcessStartInfo BuildRun(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        bool redirectStandardInput = false,
        bool redirectStandardOutput = false,
        bool redirectStandardError = false)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return BuildRun("cmd", ["/c", command], workingDirectory, environmentVariables, redirectStandardInput, redirectStandardOutput, redirectStandardError);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return BuildRun("/bin/bash", ["-c", command], workingDirectory, environmentVariables, redirectStandardInput, redirectStandardOutput, redirectStandardError);
        }
        else
        {
            throw new NotImplementedException($"Platform {RuntimeInformation.OSDescription} is not supported");
        }
    }

    public async Task<string> RunOnce(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        var startInfo = BuildRun(path, args, workingDirectory, environmentVariables, false, true, true);

        using var process = new Process { StartInfo = startInfo };
        var stdOutput = new StringBuilder();
        var stdError = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                stdOutput.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                stdError.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(stoppingToken);

        var combinedOutput = stdOutput.ToString() + stdError.ToString();

        if (process.ExitCode != 0)
        {
            throw new NativeCmdException(combinedOutput.Trim(), process.ExitCode);
        }

        return combinedOutput.Trim();
    }

    public async Task<string> RunOnce(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        var startInfo = BuildRun(command, workingDirectory, environmentVariables, false, true, true);

        using var process = new Process { StartInfo = startInfo };
        var stdOutput = new StringBuilder();
        var stdError = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                stdOutput.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                stdError.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(stoppingToken);

        var combinedOutput = stdOutput.ToString() + stdError.ToString();

        if (process.ExitCode != 0)
        {
            throw new NativeCmdException(combinedOutput.Trim(), process.ExitCode);
        }

        return combinedOutput.Trim();
    }

    public async Task<string> RunOnceAndIgnore(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        CancellationToken stoppingToken = default)
    {
        var startInfo = BuildRun(command, workingDirectory, environmentVariables, false, true, true);

        using var process = new Process { StartInfo = startInfo };
        var stdOutput = new StringBuilder();
        var stdError = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                stdOutput.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                stdError.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(stoppingToken);

        var combinedOutput = stdOutput.ToString() + stdError.ToString();
        return combinedOutput.Trim();
    }

    public async IAsyncEnumerable<ProcessEvent> RunListen(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        string? standardInput = default,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken stoppingToken = default)
    {
        var startInfo = BuildRun(path, args, workingDirectory, environmentVariables, !string.IsNullOrEmpty(standardInput), true, true);

        using var process = new Process { StartInfo = startInfo };

        var outputChannel = Channel.CreateUnbounded<ProcessEvent>();
        var writer = outputChannel.Writer;

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                writer.TryWrite(new StandardOutputProcessEvent(e.Data));
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                writer.TryWrite(new StandardErrorProcessEvent(e.Data));
            }
        };

        process.Exited += (sender, e) =>
        {
            writer.TryWrite(new ExitedProcessEvent(process.ExitCode));
            writer.Complete();
        };

        process.EnableRaisingEvents = true;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!string.IsNullOrEmpty(standardInput))
        {
            await process.StandardInput.WriteAsync(standardInput);
            process.StandardInput.Close();
        }

        await foreach (var processEvent in outputChannel.Reader.ReadAllAsync(stoppingToken))
        {
            yield return processEvent;
            
            if (processEvent is ExitedProcessEvent)
            {
                break;
            }
        }

        if (!process.HasExited)
        {
            await process.WaitForExitAsync(stoppingToken);
        }
    }

    public async Task RunListenAndLog(
        string path,
        string[] args,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        string? standardInput = default,
        CancellationToken stoppingToken = default)
    {
        using var _ = logger.BeginScopeMap<NativeCmdService>(serviceAction: nameof(RunListenAndLog), scopeMap: new Dictionary<string, object?>()
        {
            ["CmdPath"] = path,
            ["CmdArgs"] = string.Join(" ", args),
            ["WorkingDirectory"] = workingDirectory,
            ["EnvironmentVariables"] = environmentVariables?.ToDictionary()
        });

        await foreach (var processEvent in RunListen(path, args, workingDirectory, environmentVariables, standardInput, stoppingToken))
        {
            switch (processEvent)
            {
                case StandardOutputProcessEvent stdOut:
                    logger.LogTrace("{x}", stdOut.Text);
                    break;
                case StandardErrorProcessEvent stdErr:
                    logger.LogTrace("{x}", stdErr.Text);
                    break;
                case ExitedProcessEvent exited:
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

    public async IAsyncEnumerable<ProcessEvent> RunListen(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        string? standardInput = default,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken stoppingToken = default)
    {
        var startInfo = BuildRun(command, workingDirectory, environmentVariables, !string.IsNullOrEmpty(standardInput), true, true);

        using var process = new Process { StartInfo = startInfo };

        var outputChannel = Channel.CreateUnbounded<ProcessEvent>();
        var writer = outputChannel.Writer;

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                writer.TryWrite(new StandardOutputProcessEvent(e.Data));
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                writer.TryWrite(new StandardErrorProcessEvent(e.Data));
            }
        };

        process.Exited += (sender, e) =>
        {
            writer.TryWrite(new ExitedProcessEvent(process.ExitCode));
            writer.Complete();
        };

        process.EnableRaisingEvents = true;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!string.IsNullOrEmpty(standardInput))
        {
            await process.StandardInput.WriteAsync(standardInput);
            process.StandardInput.Close();
        }

        await foreach (var processEvent in outputChannel.Reader.ReadAllAsync(stoppingToken))
        {
            yield return processEvent;
            
            if (processEvent is ExitedProcessEvent)
            {
                break;
            }
        }

        if (!process.HasExited)
        {
            await process.WaitForExitAsync(stoppingToken);
        }
    }

    public async Task RunListenAndLog(
        string command,
        AbsolutePath? workingDirectory = default,
        IDictionary<string, string?>? environmentVariables = default,
        string? standardInput = default,
        CancellationToken stoppingToken = default)
    {
        using var _ = logger.BeginScopeMap<NativeCmdService>(serviceAction: nameof(RunListenAndLog), scopeMap: new Dictionary<string, object?>()
        {
            ["CmdCommand"] = command,
            ["WorkingDirectory"] = workingDirectory,
            ["EnvironmentVariables"] = environmentVariables?.ToDictionary()
        });

        string errors = "";

        await foreach (var processEvent in RunListen(command, workingDirectory, environmentVariables, standardInput, stoppingToken))
        {
            switch (processEvent)
            {
                case StandardOutputProcessEvent stdOut:
                    logger.LogTrace("{x}", stdOut.Text);
                    break;
                case StandardErrorProcessEvent stdErr:
                    logger.LogTrace("{x}", stdErr.Text);
                    if (errors != "")
                    {
                        errors += "\n";
                    }
                    errors += stdErr.Text;
                    break;
                case ExitedProcessEvent exited:
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

// Event types for process output streaming
public abstract record ProcessEvent;
public record StandardOutputProcessEvent(string Text) : ProcessEvent;
public record StandardErrorProcessEvent(string Text) : ProcessEvent;
public record ExitedProcessEvent(int ExitCode) : ProcessEvent;
