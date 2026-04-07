using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BmsTelemetry.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly AzureSettings _azure;
        private readonly GeneralSettings _general;
        private readonly NetworkSettings _network;
        private readonly LoggingSettings _logging;

        public SettingsController(
            IOptions<AzureSettings> azure,
            IOptions<GeneralSettings> general,
            IOptions<NetworkSettings> network,
            IOptions<LoggingSettings> logging)
        {
            _azure = azure.Value;
            _general = general.Value;
            _network = network.Value;
            _logging = logging.Value;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var result = new
            {
                Azure = _azure,
                General = _general,
                Network = _network,
                Logging = _logging
            };

            return Ok(result);
        }
    }
}
