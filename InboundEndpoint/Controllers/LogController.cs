using InboundEndpoint.Context;
using InboundEndpoint.DTO;
using InboundEndpoint.Repository;
using InboundEndpoint.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WinstonPuckett.PipeExtensions;

namespace InboundEndpoint.Controllers
{
    [ApiController]
    [Route("[controller]/v1")]
    public class LogController(
        LogService logService) : ControllerBase
    {
        private readonly LogService _logService = logService;

        [HttpGet(Name = "CheckHealth")]
        public IActionResult CheckHealth()
        {
            return Ok();
        }

        [HttpPost(Name = "Log")]
        public async Task<IActionResult> Log([FromBody] LogData logData)
        {
            var result = await
                logData.
                    Pipe(_logService.ValidateLogMessage).
                    PipeAsync(_logService.StoreLogRecord).
                    ContinueWith(t => t.Result.ActionResult);

            return StringToActionResult(result);
        }

        private IActionResult StringToActionResult(string actionResult)
        {
            return actionResult switch
            {
                "" => Ok(),
                _ => BadRequest(actionResult)
            };
        }

    }
}
