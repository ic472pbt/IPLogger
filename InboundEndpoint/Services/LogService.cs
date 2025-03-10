using InboundEndpoint.DTO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using InboundEndpoint.Repository;
using InboundEndpoint.Model;
using InboundEndpoint.Controllers;

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
               if (string.IsNullOrEmpty(logData.IPAddress) || !logData.IsValidIP())
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


           /*public async Task<LogDataWrapper> StoreLogRecord(LogDataWrapper logDataWrapper)
           {
               if (!string.IsNullOrEmpty(logDataWrapper.ActionResult))
               {
                   return logDataWrapper;
               }
               try
               {
                   var user = await userEntity.GetOrCreateUserById(logDataWrapper.LogData.UserId);
                   if(logDataWrapper.LogData.IsIPV4()){
                       user.ConnectionsV4.Add(new ConnectionDataV4() { IpAddress = logDataWrapper.LogData.IPAddressInt32(), ConnectionTime = logDataWrapper.DateTime.ToUniversalTime(), User = user });
                   }
                   else
                   {
                       var (high, low) = logDataWrapper.LogData.IPAddressInt128();
                       user.ConnectionsV6.Add(new ConnectionDataV6() { IpAddressHigh = high, IpAddressLow = low, ConnectionTime = logDataWrapper.DateTime.ToUniversalTime(), User = user });
                   }
                   await userEntity.Save();
                   return logDataWrapper with { User = user };
               }
               catch (Exception e)
               {
                   logger.LogError(e, "Error storing log record");
                   // TODO: Implement 500 error status code
                   return logDataWrapper with { ActionResult = e.Message };
               }
           }
          // public async Task<LogDataWrapper> StoreUserIPLog(LogDataWrapper logDataWrapper)
        //   {

           }*/
       }
}
