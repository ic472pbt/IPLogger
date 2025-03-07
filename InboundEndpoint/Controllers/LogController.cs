using InboundEndpoint.DTO;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using WinstonPuckett.PipeExtensions;

namespace InboundEndpoint.Controllers
{
    [ApiController]
    [Route("[controller]\v1")]
    public class LogController : ControllerBase
    {

        private readonly ILogger<LogController> _logger;

        public 
            LogController(ILogger<LogController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "CheckHealth")]
        public IActionResult CheckHealth()
        {
            return Ok();
        }

        [HttpPost(Name = "SendDownStream")]
        public async Task<IActionResult> SendDownStream([FromBody] LogData logData)
        {
            var result = 
                logData.
                    Pipe(Validate).
                    ActionResult;

            _logger.LogInformation("Sending weather forecast to downstream service");
            return result;
        }

        private LogDataWrapper Validate(LogData logData)
        {
            if (logData.UserId == 0)
            {
                _logger.LogError("UserId is required {0}", logData.UserId);
                return new LogDataWrapper(LogData: logData, ActionResult: BadRequest(), SessionId: 0);
            }
            if (string.IsNullOrEmpty(logData.IPAddress) || !Regex.IsMatch(logData.IPAddress, @"^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
            {
                _logger.LogError("Valid IPAddress is required");
                return new LogDataWrapper(LogData: logData, ActionResult: BadRequest(), SessionId: 0);
            }
            return new LogDataWrapper(LogData: logData, ActionResult: Ok(), SessionId: 0);
        }
    }
}
