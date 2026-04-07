using Microsoft.AspNetCore.Mvc;

namespace BmsTelemetry.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CapabilitiesController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            var capabilities = new[]
            {
                "GetSettings",
                "AckAlarms",
                "SetHvacService"
            };

            return Ok(new { capabilities });
        }
    }
}
