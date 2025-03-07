using Microsoft.AspNetCore.Mvc;

namespace InboundEndpoint.DTO
{
    public record LogDataWrapper(LogData LogData, IActionResult ActionResult, int SessionId, int EventId);
}
