using BlazorApp1.Models;
using BlazorApp1.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlazorApp1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherController(IWeatherService weatherService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<WeatherForecast[]>> GetForecasts()
    {
        var forecasts = await weatherService.GetForecastsAsync();
        return Ok(forecasts);
    }
}
