using InboundEndpoint.Data;
using InboundEndpoint.DTO;
using InboundEndpoint.Infrastructure.Postgres.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using WinstonPuckett.PipeExtensions;

namespace InboundEndpoint.Controllers
{
    [ApiController]
    [Route("[controller]\v1")]
    public class LogController(ILogger<LogController> logger, AppDbContext appDbContext, Session session, IDistributedCache distributedCache) : ControllerBase
    {
        private readonly ILogger<LogController> _logger = logger;
        private readonly AppDbContext _appDbContext = appDbContext;
        private readonly Session _session = session;
        private static readonly ConcurrentDictionary<(long UserId, int SessionId), int> EventIds = new();
        private readonly IDistributedCache _distributedCache = distributedCache;

        [HttpGet(Name = "CheckHealth")]
        public IActionResult CheckHealth()
        {
            return Ok();
        }

        [HttpPost(Name = "SendDownstream")]
        public async Task<IActionResult> SendDownstream([FromBody] LogData logData)
        {
            var result = await
                logData.
                    Pipe(Validate).
                    PipeAsync(GetSessionAndEventId).
                    PipeAsync(PushToRedis).
                    ContinueWith(t => t.Result.ActionResult);

            return result;
        }

        private LogDataWrapper Validate(LogData logData)
        {
            DateTime timeStamp = DateTime.Now;
            if (logData.UserId == 0)
            {
                _logger.LogError("UserId is required {UserId}", logData.UserId);
                return new LogDataWrapper(LogData: logData, DateTime: timeStamp, ActionResult: BadRequest(), SessionId: 0, EventId: 0);
            }
            if (string.IsNullOrEmpty(logData.IPAddress) || !Regex.IsMatch(logData.IPAddress, @"^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
            {
                _logger.LogError("Valid IPAddress is required");
                return new LogDataWrapper(LogData: logData, DateTime: DateTime.Now, ActionResult: BadRequest(), SessionId: 0, EventId: 0);
            }
            return new LogDataWrapper(LogData: logData, DateTime: DateTime.Now, ActionResult: Ok(), SessionId: 0, EventId: 0);
        }

        private async Task<LogDataWrapper> GetSessionAndEventId(LogDataWrapper logDataWrapper)
        {
            if (logDataWrapper.ActionResult is not OkResult)
            {
                return logDataWrapper;
            }
            var sessionId = await _session.GetNewSessionId(_appDbContext, logDataWrapper.LogData.UserId);
            return logDataWrapper with { SessionId = sessionId, EventId = GetEventId(logDataWrapper.LogData.UserId, sessionId) };
        }

        private static int GetEventId(long UserId, int SessionId)
        {
            var key = (UserId, SessionId);
            return EventIds.AddOrUpdate(key, 1, (k, v) => v + 1);
        }

        private async Task<LogDataWrapper> PushToRedis(LogDataWrapper logDataWrapper)
        {
            if (logDataWrapper.ActionResult is not OkResult)
            {
                return logDataWrapper;
            }
            var logData = logDataWrapper.LogData;
            var sessionId = logDataWrapper.SessionId;
            var eventId = logDataWrapper.EventId;
            var key = $"{logData.UserId}_{sessionId}_{eventId}";
            var value = JsonConvert.SerializeObject(logDataWrapper.ToLogDataMessage());
            await _distributedCache.SetStringAsync(key, value);
            return logDataWrapper;
        }
    }
}
