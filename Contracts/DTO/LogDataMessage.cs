
using Contracts.Domain;

namespace Contracts.DTO
{
    public record class LogDataMessage(LogData LogData, DateTimeOffset DateTime);
}
