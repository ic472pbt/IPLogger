using Contracts.DTO;
using Infrastructure.Postgres.Context;
using Infrastructure.Postgres.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql.Replication;

namespace Infrastructure.Postgres.Repository
{
    public class UserEntity(ILogger logger, AppDbContext appDbContext)
    {
        private readonly AppDbContext _appDbContext = appDbContext;
        public void AddUser(UserData userData)
        {
            _appDbContext.Users.Add(userData);
        }
        public async Task<int> Save()
        {
            return await _appDbContext.SaveChangesAsync();
        }
        public async Task<UserData> GetOrCreateUserById(long userId)
        {
            UserData? user = await _appDbContext.Users.FindAsync(userId);
            if (user is null)
            {
                user = new UserData() { UserId = userId };
                AddUser(user);

                logger.LogInformation("User {UserId} created", userId);
            }
            return user;
        }
        public async Task<bool> UserExists(long userId)
        {
            return await _appDbContext.Users.AnyAsync(u => u.UserId == userId);
        }

        public async Task<LogDataMessage> StoreLogRecord(LogDataMessage logDataMessage)
           {
               try
               {
                   var user = await GetOrCreateUserById(logDataMessage.LogData.UserId);
                   if(logDataMessage.LogData.IsIPV4()){
                       user.ConnectionsV4.Add(new ConnectionDataV4() { IpAddress = logDataMessage.LogData.IPAddressInt32(), ConnectionTime = logDataMessage.DateTime.ToUniversalTime(), User = user });
                   }
                   else
                   {
                       var (high, low) = logDataMessage.LogData.IPAddressInt128();
                       user.ConnectionsV6.Add(new ConnectionDataV6() { IpAddressHigh = high, IpAddressLow = low, ConnectionTime = logDataMessage.DateTime.ToUniversalTime(), User = user });
                   }
                   user.LastEventId = logDataMessage.LogData.EventId;
                // await Save();
                    return logDataMessage;
               }
               catch (Exception e)
               {
                   logger.LogError(e, "Error storing log record");
                   // TODO: Implement 500 error status code
                   return logDataMessage;
               }
           }
    }
}
