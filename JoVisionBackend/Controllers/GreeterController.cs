using Microsoft.AspNetCore.Mvc;

namespace MyFirstApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GreeterController : ControllerBase
    {
        [HttpGet("greet")]
        public IActionResult GetGreeting([FromQuery] string? name)
        {
            string displayName = string.IsNullOrWhiteSpace(name) ? "anonymous" : name;

            return Ok($"Hello {displayName}");
        }
    }
}