using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

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

    // Use 127.0.0.1 instead of localhost to avoid IPv6/IPv4 resolution issues
    // Python http.server binds to 127.0.0.1 explicitly
    public string BaseUrl => $"http://127.0.0.1:{_port}";

    public WebAppTestHost(ITestOutputHelper output, string webApiUrl, int port = 0)
    {
        _output = output;
        _webApiUrl = webApiUrl;
        _port = port == 0 ? GetAvailablePort() : port;
    }

    /// <summary>
    /// Gets an available port by binding to port 0 and letting the OS assign one.
    /// This ensures no port collisions when running tests in parallel.
    /// </summary>
    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async Task StartAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(60);

        _output.WriteLine($"[WEBAPP] Starting WebApp on {BaseUrl}...");

        // Blazor WASM build output structure:
        // - bin/Release/net10.0/wwwroot/_framework/ contains compiled WASM assemblies
        // - src/Presentation.WebApp/wwwroot/ contains static files (index.html, css/)
        // We need to serve from bin output and copy source static files there
        // NOTE: Using Release build to avoid Hot Reload module dependency issues
        
        var webAppOutputDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Presentation.WebApp", "bin", "Release", "net10.0", "wwwroot"));
            
        var webAppSourceDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Presentation.WebApp", "wwwroot"));

        _output.WriteLine($"[WEBAPP] Output wwwroot dir: {webAppOutputDir}");
        _output.WriteLine($"[WEBAPP] Source wwwroot dir: {webAppSourceDir}");

        if (!Directory.Exists(webAppOutputDir))
        {
            throw new DirectoryNotFoundException($"WebApp output directory not found at {webAppOutputDir}. Ensure Presentation.WebApp is built.");
        }

        if (!Directory.Exists(webAppSourceDir))
        {
            throw new DirectoryNotFoundException($"WebApp source directory not found at {webAppSourceDir}. Ensure Presentation.WebApp project exists.");
        }

        // Copy source static files (index.html, css/) to bin output if not already there
        CopySourceFilesToOutput(webAppSourceDir, webAppOutputDir);

        // Verify index.html exists in output
        var indexHtml = Path.Combine(webAppOutputDir, "index.html");
        if (!File.Exists(indexHtml))
        {
            throw new FileNotFoundException($"index.html not found at {indexHtml}");
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
    
    private void CopySourceFilesToOutput(string sourceDir, string outputDir)
    {
        // Copy all files from source wwwroot to output wwwroot (preserving directory structure)
        // This copies index.html, css/, etc. but doesn't overwrite _framework/
        foreach (var sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            var destFile = Path.Combine(outputDir, relativePath);
            var destDir = Path.GetDirectoryName(destFile);
            
            if (destDir != null && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }
            
            // Only copy if source is newer or destination doesn't exist
            if (!File.Exists(destFile) || File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(destFile))
            {
                File.Copy(sourceFile, destFile, overwrite: true);
                _output.WriteLine($"[WEBAPP] Copied {relativePath}");
            }
        }
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

        // SPA fallback: serve index.html for unknown routes (client-side routing)
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"serve -p {_port} -d \"{webRoot}\" --default-extensions:html --fallback-file index.html",
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
            Arguments = $"-m http.server {_port} --bind 127.0.0.1",
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
