using BlazorAppSampleTemplate.Shared.Models;
using BlazorAppSampleTemplate.Shared.Services;

namespace BlazorAppSampleTemplate.Services;

/// <summary>
/// Server-side implementation that generates weather data.
/// Injected into the API controller.
/// </summary>
public class ServerWeatherService : IWeatherService
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", 
        "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    public async Task<WeatherForecast[]> GetForecastAsync()
    {
        // Simulate async database call
        await Task.Delay(100);

        var startDate = DateOnly.FromDateTime(DateTime.Now);
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = startDate.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }).ToArray();
    }
}
