using Contracts.Domain;

namespace InboundEndpoint.DTO
{
    public record LogDataWrapper(LogData LogData, DateTimeOffset DateTime, string ActionResult);

}
