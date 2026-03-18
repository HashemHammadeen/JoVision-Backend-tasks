using Microsoft.AspNetCore.Mvc;

namespace MyFirstApi.Controllers
{
    [ApiController]
    [Route("birthdate")]
    public class BirthDateController : ControllerBase
    {
        [HttpGet("age")]
        public IActionResult GetAge(
            [FromQuery] string? name, 
            [FromQuery] int? years, 
            [FromQuery] int? months, 
            [FromQuery] int? days)
        {
            string displayName = string.IsNullOrWhiteSpace(name) ? "anonymous" : name;

            if (years == null || months == null || days == null)
            {
                return Ok($"Hello {displayName}, I can’t calculate your age without knowing your birthdate!");
            }

            try 
            {
                DateTime birthDate = new DateTime(years.Value, months.Value, days.Value);
                DateTime today = DateTime.Today;

                int age = today.Year - birthDate.Year;

                // Go back one year if the birthday hasn't happened yet this year
                if (birthDate.Date > today.AddYears(-age)) 
                {
                    age--;
                }

                return Ok($"Hello {displayName}, your age is {age}");
            }
            catch (ArgumentOutOfRangeException)
            {
                return BadRequest("The date provided is invalid.");
            }
        }
    }
}