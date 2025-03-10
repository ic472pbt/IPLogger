using InboundEndpoint.DTO;
using InboundEndpoint.Services;
using Infrastructure.Kafka;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WinstonPuckett.PipeExtensions;

namespace InboundEndpoint.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LogController(
        ILogger<LogController> logger,
        LogService logService,
        Connector kafkaConnector
        ) : ControllerBase
    {

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
                    Pipe(logService.ValidateLogMessage).
                    PipeAsync(ProduceLogMessage).
                    ContinueWith(t => t.Result.ActionResult);

            return StringToActionResult(result);
        }

        private async Task<LogDataWrapper> ProduceLogMessage(LogDataWrapper logDataWrapper)
        {
            if(logDataWrapper.ActionResult != "")
            {
                return logDataWrapper;
            }
            try
            {
                await kafkaConnector.ProduceMessageAsync(logDataWrapper.LogData.UserId.ToString(), JsonConvert.SerializeObject(logDataWrapper.LogData));
                return logDataWrapper;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error producing message");
                return logDataWrapper with { ActionResult = "Error producing message" };
            }
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
