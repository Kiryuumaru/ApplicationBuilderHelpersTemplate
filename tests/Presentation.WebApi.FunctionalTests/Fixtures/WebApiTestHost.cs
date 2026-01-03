using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.SignalR.Client;

namespace Presentation.WebApi.FunctionalTests.Fixtures;

/// <summary>
/// Test host that runs the WebApi application as a subprocess for functional testing.
/// Since WebApi uses ApplicationBuilderHelpers CLI pattern, WebApplicationFactory doesn't work.
/// Instead, we start the actual application and test against it via HTTP.
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
        _port = port == 0 ? GetRandomPort() : port;
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    private static int GetRandomPort()
    {
        // Use a random port in the ephemeral range
        return Random.Shared.Next(49152, 65535);
    }

    /// <summary>
    /// Starts the WebApi application as a subprocess.
    /// </summary>
    public async Task StartAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(60);

        _output.WriteLine($"[HOST] Starting WebApi on {BaseUrl}...");

        // Find the WebApi executable - it should be built already
        var exePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Presentation.WebApi", "bin", "Debug", "net10.0", "Presentation.WebApi.exe"));

        // Fallback to dll if exe doesn't exist (Linux/macOS)
        if (!File.Exists(exePath))
        {
            exePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "Presentation.WebApi", "bin", "Debug", "net10.0", "Presentation.WebApi.dll"));
        }

        _output.WriteLine($"[HOST] Using executable: {exePath}");

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"WebApi executable not found at {exePath}. Build the WebApi project first.");
        }

        var isExe = exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        var startInfo = new ProcessStartInfo
        {
            FileName = isExe ? exePath : "dotnet",
            // Pass --urls argument to configure the listening URL
            Arguments = isExe ? $"--urls {BaseUrl}" : $"\"{exePath}\" --urls {BaseUrl}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exePath)
        };

        // Also set environment variables as fallback
        startInfo.Environment["ASPNETCORE_URLS"] = BaseUrl;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        // Disable launch settings to avoid conflicts
        startInfo.Environment["DOTNET_LAUNCH_PROFILE"] = "";
        // Use a unique in-memory database for each test run to ensure clean state
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

        _output.WriteLine($"[HOST] Process started (PID: {_process.Id})");

        // Wait for the server to be ready
        await WaitForServerReady(timeout.Value);

        _output.WriteLine("[HOST] WebApi started successfully");
    }

    private async Task WaitForServerReady(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            try
            {
                var response = await _httpClient.GetAsync("/");
                if (response.IsSuccessStatusCode || (int)response.StatusCode < 500)
                {
                    _output.WriteLine($"[HOST] Server responded with {response.StatusCode} after {sw.ElapsedMilliseconds}ms");
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

    /// <summary>
    /// Creates a SignalR HubConnection to the specified hub.
    /// </summary>
    public HubConnection CreateHubConnection(string hubPath)
    {
        _output.WriteLine($"[HOST] Creating SignalR connection to {hubPath}");

        var hubUrl = new Uri(new Uri(BaseUrl), hubPath);

        return new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .Build();
    }

    /// <summary>
    /// Creates an HTTP client with authentication.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string token)
    {
        var client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        _output.WriteLine("[HOST] Shutting down WebApi...");

        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[HOST] Error killing process: {ex.Message}");
            }
        }

        _process?.Dispose();
        _httpClient.Dispose();

        _output.WriteLine("[HOST] WebApi stopped");
    }
}
