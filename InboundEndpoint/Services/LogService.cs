using InboundEndpoint.DTO;
using InboundEndpoint.Controllers;
using Contracts.Domain;
using Infrastructure.Helpers;

namespace InboundEndpoint.Services
{
       public class LogService(ILogger<LogController> logger)
       {
           public LogDataWrapper ValidateLogMessage(LogData logData)
           {
               DateTime timeStamp = DateTime.Now;
               if (logData.UserId == 0)
               {
                   string errorMessage = "UserId is required";
                   logger.LogError("{ErrorMessage} {UserId}", errorMessage, logData.UserId);
                   return new LogDataWrapper(
                       LogData: logData,
                       DateTime: timeStamp,
                       ActionResult: $"{errorMessage} {logData.UserId}"
                   );
               }
               if (string.IsNullOrEmpty(logData.IPAddress) || !IpHelper.IsValidIP(logData.IPAddress))
               {
                   string errorMessage = "Valid IPAddress is required";
                   logger.LogError("{ErrorMessage} {IPAddress}", errorMessage, logData.IPAddress);
                   return new LogDataWrapper(
                       LogData: logData,
                       DateTime: DateTimeOffset.Now,
                       ActionResult: $"{errorMessage} {logData.IPAddress}"
                   );
               }
               return new LogDataWrapper(LogData: logData, DateTime: DateTimeOffset.Now, ActionResult: string.Empty);
           }         
       }
}
