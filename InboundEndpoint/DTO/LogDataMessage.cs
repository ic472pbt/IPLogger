using Microsoft.AspNetCore.Mvc;

namespace InboundEndpoint.DTO
{
    public record class LogDataMessage(LogData LogData, DateTime DateTime, int SessionId, int EventId);
}
