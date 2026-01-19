using BlazorAppSampleTemplate.Shared.Models;

namespace BlazorAppSampleTemplate.Shared.Services;

public interface IWeatherService
{
    Task<WeatherForecast[]> GetForecastAsync();
}
