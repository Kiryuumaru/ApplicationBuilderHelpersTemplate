using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Presentation.WebApp.FunctionalTests.Fixtures;

/// <summary>
/// Test host that runs the WebApi application as a subprocess for functional testing.
/// </summary>
public class WebApiTestHost : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _httpClient;
    private readonly int _port;
    private Process? _process;

    public string BaseUrl => $"http://localhost:{_port}";
    public HttpClient HttpClient => _httpClient;

    public WebApiTestHost(ITestOutputHelper output, int port = 0)
    {
        _output = output;
        _port = port == 0 ? GetAvailablePort() : port;
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
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

        _output.WriteLine($"[WEBAPI] Starting WebApi on {BaseUrl}...");

        var webApiOutputDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Presentation.WebApi", "bin", "Debug", "net10.0"));

        _output.WriteLine($"[WEBAPI] Output dir: {webApiOutputDir}");

        if (!Directory.Exists(webApiOutputDir))
        {
            throw new DirectoryNotFoundException($"WebApi output directory not found at {webApiOutputDir}. Build the WebApi project first.");
        }

        var exePath = Directory.GetFiles(webApiOutputDir, "*.runtimeconfig.json")
            .Select(rc => Path.GetFileName(rc).Replace(".runtimeconfig.json", ""))
            .Select(name => Path.Combine(webApiOutputDir, name + ".exe"))
            .FirstOrDefault(File.Exists);

        if (exePath == null)
        {
            exePath = Directory.GetFiles(webApiOutputDir, "*.runtimeconfig.json")
                .Select(rc => Path.GetFileName(rc).Replace(".runtimeconfig.json", ""))
                .Select(name => Path.Combine(webApiOutputDir, name + ".dll"))
                .FirstOrDefault(File.Exists);
        }

        if (exePath == null || !File.Exists(exePath))
        {
            throw new FileNotFoundException($"WebApi executable not found in {webApiOutputDir}. Build the WebApi project first.");
        }

        var isExe = exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        var workDir = Path.GetDirectoryName(exePath);
        var args = isExe ? $"--urls {BaseUrl}" : $"\"{exePath}\" --urls {BaseUrl}";
        var fileName = isExe ? exePath : "dotnet";

        _output.WriteLine($"[WEBAPI] Starting: {fileName} {args}");

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workDir
        };

        startInfo.Environment["ASPNETCORE_URLS"] = BaseUrl;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["DOTNET_LAUNCH_PROFILE"] = "";
        startInfo.Environment["SQLITE_CONNECTION_STRING"] = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";

        _process = new Process { StartInfo = startInfo };

        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _output.WriteLine($"[WEBAPI] {e.Data}");
            }
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _output.WriteLine($"[WEBAPI ERR] {e.Data}");
            }
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        _output.WriteLine($"[WEBAPI] Process started (PID: {_process.Id})");

        await WaitForServerReady(timeout.Value);

        _output.WriteLine("[WEBAPI] Started successfully");
    }

    private async Task WaitForServerReady(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            if (_process != null && _process.HasExited)
            {
                throw new InvalidOperationException($"WebApi process exited unexpectedly with code {_process.ExitCode}.");
            }

            try
            {
                var response = await _httpClient.GetAsync("/");
                if (response.IsSuccessStatusCode || (int)response.StatusCode < 500)
                {
                    _output.WriteLine($"[WEBAPI] Server responded with {response.StatusCode} after {sw.ElapsedMilliseconds}ms");
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Server not ready yet
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"WebApi did not start within {timeout.TotalSeconds}s");
    }

    public async ValueTask DisposeAsync()
    {
        _output.WriteLine("[WEBAPI] Shutting down...");

        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[WEBAPI] Error killing process: {ex.Message}");
            }
        }

        _process?.Dispose();
        _httpClient.Dispose();

        _output.WriteLine("[WEBAPI] Stopped");
    }
}
