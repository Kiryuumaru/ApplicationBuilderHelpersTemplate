using BlazorAppSampleTemplate.Shared.Models;
using BlazorAppSampleTemplate.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlazorAppSampleTemplate.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherController(IWeatherService weatherService) : ControllerBase
{
    [HttpGet]
    public async Task<WeatherForecast[]> Get()
    {
        return await weatherService.GetForecastAsync();
    }
}
