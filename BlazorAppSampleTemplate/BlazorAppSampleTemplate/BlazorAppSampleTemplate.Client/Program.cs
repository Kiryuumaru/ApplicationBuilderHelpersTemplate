using BlazorAppSampleTemplate.Client.Services;
using BlazorAppSampleTemplate.Shared.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register HttpClient with base address
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Register Weather service
builder.Services.AddScoped<IWeatherService, WeatherService>();

await builder.Build().RunAsync();
