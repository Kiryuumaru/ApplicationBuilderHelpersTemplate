using BlazorApp1.Models;

namespace BlazorApp1.Services;

public interface IWeatherService
{
    Task<WeatherForecast[]> GetForecastsAsync();
}
