using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {


        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<string> Get()
        {
            return [];
        }
    }
}
