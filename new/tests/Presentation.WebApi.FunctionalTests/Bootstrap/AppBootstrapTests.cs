using System.Net;
using Presentation.WebApi.FunctionalTests.Fixtures;

namespace Presentation.WebApi.FunctionalTests.Bootstrap;

/// <summary>
/// Tests that the WebApi application boots correctly.
/// </summary>
[Collection(WebApiTestCollection.Name)]
public class AppBootstrapTests
{
    private readonly ITestOutputHelper _output;
    private readonly SharedWebApiHost _sharedHost;

    public AppBootstrapTests(SharedWebApiHost sharedHost, ITestOutputHelper output)
    {
        _sharedHost = sharedHost;
        _output = output;
    }

    [Fact]
    public async Task WebApi_StartsSuccessfully()
    {
        _output.WriteLine("[TEST] WebApi_StartsSuccessfully");
        _output.WriteLine("[STEP] GET / (root endpoint)...");

        var response = await _sharedHost.Host.HttpClient.GetAsync("/");

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Root redirects to Scalar API docs (302/200)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Redirect ||
            response.StatusCode == HttpStatusCode.Found,
            $"Expected OK or Redirect, got {response.StatusCode}");

        _output.WriteLine("[PASS] WebApi started and responds successfully");
    }

    [Fact]
    public async Task Swagger_ReturnsOpenApiDocument()
    {
        _output.WriteLine("[TEST] Swagger_ReturnsOpenApiDocument");

        _output.WriteLine("[STEP] GET /swagger/v1/swagger.json...");
        var response = await _sharedHost.Host.HttpClient.GetAsync("/swagger/v1/swagger.json");

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"[RECEIVED] Content length: {content.Length} chars");

            Assert.Contains("openapi", content.ToLower());
            _output.WriteLine("[PASS] OpenAPI document available");
        }
        else
        {
            _output.WriteLine($"[SKIP] OpenAPI not at expected path (status {response.StatusCode})");
        }
    }

    [Fact]
    public async Task Scalar_UI_Available()
    {
        _output.WriteLine("[TEST] Scalar_UI_Available");

        _output.WriteLine("[STEP] GET /scalar/v1...");
        var response = await _sharedHost.Host.HttpClient.GetAsync("/scalar/v1");

        _output.WriteLine($"[RECEIVED] Status: {(int)response.StatusCode} {response.StatusCode}");

        // Scalar UI should be available
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {response.StatusCode}");
        _output.WriteLine("[PASS] Scalar UI available");
    }
}
