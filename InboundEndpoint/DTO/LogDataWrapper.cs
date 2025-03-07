using Microsoft.AspNetCore.Mvc;

namespace InboundEndpoint.DTO
{
    public record LogDataWrapper(LogData LogData, DateTime DateTime, IActionResult ActionResult, int SessionId, int EventId)
    {
        public LogDataMessage ToLogDataMessage()
        {
            return new(LogData, DateTime, SessionId, EventId);
        }
    }
}
