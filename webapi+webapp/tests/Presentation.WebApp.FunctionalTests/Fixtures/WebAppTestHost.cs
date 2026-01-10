using System.Diagnostics;

namespace Presentation.WebApp.FunctionalTests.Fixtures;

/// <summary>
/// Test host that runs the Blazor WebApp using dotnet serve.
/// Since Blazor WASM is a static site, we use a simple HTTP server.
/// </summary>
public class WebAppTestHost : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly int _port;
    private readonly string _webApiUrl;
    private Process? _process;

    public string BaseUrl => $"http://localhost:{_port}";

    public WebAppTestHost(ITestOutputHelper output, string webApiUrl, int port = 0)
    {
        _output = output;
        _webApiUrl = webApiUrl;
        _port = port == 0 ? GetRandomPort() : port;
    }

    private static int GetRandomPort()
    {
        return Random.Shared.Next(49152, 65535);
    }

    public async Task StartAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(60);

        _output.WriteLine($"[WEBAPP] Starting WebApp on {BaseUrl}...");

        var webAppOutputDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Presentation.WebApp", "bin", "Debug", "net10.0", "wwwroot"));

        _output.WriteLine($"[WEBAPP] Output dir: {webAppOutputDir}");

        if (!Directory.Exists(webAppOutputDir))
        {
            // Try publish output for browser-wasm
            webAppOutputDir = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "Presentation.WebApp", "bin", "Debug", "net10.0", "publish", "wwwroot"));

            _output.WriteLine($"[WEBAPP] Trying publish dir: {webAppOutputDir}");
        }

        if (!Directory.Exists(webAppOutputDir))
        {
            throw new DirectoryNotFoundException($"WebApp output directory not found. Build and publish the WebApp project first.");
        }

        // Use dotnet serve or a simple Python HTTP server as fallback
        var dotnetServe = await FindDotnetServeAsync();
        
        if (dotnetServe)
        {
            await StartWithDotnetServeAsync(webAppOutputDir, timeout.Value);
        }
        else
        {
            await StartWithSimpleServerAsync(webAppOutputDir, timeout.Value);
        }

        _output.WriteLine("[WEBAPP] Started successfully");
    }

    private async Task<bool> FindDotnetServeAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "tool list -g",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p != null)
            {
                var output = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                return output.Contains("dotnet-serve", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Ignore
        }

        return false;
    }

    private async Task StartWithDotnetServeAsync(string webRoot, TimeSpan timeout)
    {
        _output.WriteLine("[WEBAPP] Using dotnet-serve");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"serve -p {_port} -d \"{webRoot}\" --default-extensions:html",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        await StartServerProcess(startInfo, timeout);
    }

    private async Task StartWithSimpleServerAsync(string webRoot, TimeSpan timeout)
    {
        _output.WriteLine("[WEBAPP] Using Python http.server");

        var startInfo = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"-m http.server {_port}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = webRoot
        };

        await StartServerProcess(startInfo, timeout);
    }

    private async Task StartServerProcess(ProcessStartInfo startInfo, TimeSpan timeout)
    {
        _process = new Process { StartInfo = startInfo };

        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _output.WriteLine($"[WEBAPP] {e.Data}");
            }
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _output.WriteLine($"[WEBAPP ERR] {e.Data}");
            }
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        _output.WriteLine($"[WEBAPP] Process started (PID: {_process.Id})");

        await WaitForServerReady(timeout);
    }

    private async Task WaitForServerReady(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        using var client = new HttpClient();

        while (sw.Elapsed < timeout)
        {
            if (_process != null && _process.HasExited)
            {
                throw new InvalidOperationException($"WebApp server process exited unexpectedly with code {_process.ExitCode}.");
            }

            try
            {
                var response = await client.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode || (int)response.StatusCode < 500)
                {
                    _output.WriteLine($"[WEBAPP] Server responded with {response.StatusCode} after {sw.ElapsedMilliseconds}ms");
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Server not ready yet
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"WebApp server did not start within {timeout.TotalSeconds}s");
    }

    public async ValueTask DisposeAsync()
    {
        _output.WriteLine("[WEBAPP] Shutting down...");

        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[WEBAPP] Error killing process: {ex.Message}");
            }
        }

        _process?.Dispose();

        _output.WriteLine("[WEBAPP] Stopped");
    }
}
