using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Presentation.WebApp.Tests;

public class WebAppTestFixture : IDisposable
{
    private Process? _process;
    public string ServerAddress { get; private set; } = null!;

    public WebAppTestFixture()
    {
        var assemblyName = "Presentation.WebApp";
        // Assuming we are in bin/Debug/net10.0
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var dllPath = Path.Combine(solutionRoot, "src", assemblyName, "bin", "Debug", "net10.0", $"{assemblyName}.dll");

        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException($"Could not find {dllPath}. Make sure to build the project first.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec \"{dllPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        
        startInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ConnectionStrings:DefaultConnection"] = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";
        startInfo.Environment["SQLITE_CONNECTION_STRING"] = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";

        _process = new Process { StartInfo = startInfo };
        _process.Start();

        // Read output to find the URL
        var tcs = new TaskCompletionSource<string>();
        
        _ = Task.Run(async () => {
            while (!_process.StandardOutput.EndOfStream)
            {
                var line = await _process.StandardOutput.ReadLineAsync();
                if (line != null)
                {
                    Console.WriteLine($"[App]: {line}"); // Forward output for debugging
                    // Match patterns like "[Lifetime] Now listening on: http://..." or "Now listening on: http://..."
                    var match = Regex.Match(line, @"Now listening on:\s*(http://\S+)");
                    if (match.Success)
                    {
                        tcs.TrySetResult(match.Groups[1].Value);
                    }
                }
            }
        });
        
        _ = Task.Run(async () => {
             while (!_process.StandardError.EndOfStream)
             {
                 var line = await _process.StandardError.ReadLineAsync();
                 if (line != null) Console.WriteLine($"[App Error]: {line}");
             }
        });

        if (tcs.Task.Wait(TimeSpan.FromSeconds(30)))
        {
            ServerAddress = tcs.Task.Result;
        }
        else
        {
            _process.Kill();
            throw new Exception("Timed out waiting for server to start.");
        }
    }

    public void Dispose()
    {
        if (_process != null && !_process.HasExited)
        {
            _process.Kill();
            _process.Dispose();
        }
    }
}
