using InboundEndpoint.Model;
using Microsoft.AspNetCore.Mvc;

namespace InboundEndpoint.DTO
{
    public record LogDataWrapper(LogData LogData, DateTimeOffset DateTime, string ActionResult);

}
