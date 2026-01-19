using System.Net.Http.Json;
using BlazorAppSampleTemplate.Shared.Models;
using BlazorAppSampleTemplate.Shared.Services;

namespace BlazorAppSampleTemplate.Client.Services;

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;

    public WeatherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WeatherForecast[]> GetForecastAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<WeatherForecast[]>("api/weather");
        return result ?? [];
    }
}
