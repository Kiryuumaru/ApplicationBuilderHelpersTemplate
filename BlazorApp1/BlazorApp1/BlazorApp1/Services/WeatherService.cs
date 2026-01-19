using BlazorApp1.Models;

namespace BlazorApp1.Services;

public class WeatherService : IWeatherService
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild",
        "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    public async Task<WeatherForecast[]> GetForecastsAsync()
    {
        // Simulate async work (e.g., database call)
        await Task.Delay(500);

        var startDate = DateOnly.FromDateTime(DateTime.Now);
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = startDate.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }).ToArray();
    }
}
