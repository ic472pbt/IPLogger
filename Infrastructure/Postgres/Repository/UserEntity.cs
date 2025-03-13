using Contracts.DTO;
using Infrastructure.Helpers;
using Infrastructure.Postgres.Context;
using Infrastructure.Postgres.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql.Replication;

namespace Infrastructure.Postgres.Repository
{
    public class LastEventData()
    {
        public required UserData UserData { get; set; }
        public ConnectionDataV4? ConnectionDataV4 { get; set; }
        public ConnectionDataV6? ConnectionDataV6 { get; set; }
    }
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

        public async Task<LastEventData> StoreLogRecord(LogDataMessage logDataMessage)
        {
            try
            {
                var user = await GetOrCreateUserById(logDataMessage.LogData.UserId);
                LastEventData lastEventData = new() { UserData = user};
                if (IpHelper.IsIPV4(logDataMessage.LogData.IPAddress))
                {
                    var connection = new ConnectionDataV4()
                    {
                        IpAddress = IpHelper.IPAddressInt32(logDataMessage.LogData.IPAddress),
                        ConnectionTime = logDataMessage.DateTime.ToUniversalTime(),
                        User = user
                    };
                    user.ConnectionsV4.Add(connection);
                    user.LastEventIsIPv6 = false;
                    lastEventData.ConnectionDataV4 = connection;
                }
                else
                {
                    var (high, low) = IpHelper.IPAddressInt128(logDataMessage.LogData.IPAddress);
                    var connection = new ConnectionDataV6()
                    {
                        IpAddressHigh = high,
                        IpAddressLow = low,
                        ConnectionTime = logDataMessage.DateTime.ToUniversalTime(),
                        User = user
                    };
                    user.ConnectionsV6.Add(connection);
                    user.LastEventIsIPv6 = true;
                    lastEventData.ConnectionDataV6 = connection;
                }
                user.LastEventId = logDataMessage.LogData.EventId;
                // await Save();
                return lastEventData;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error storing log record");
                // TODO: Implement 500 error status code
                throw;
            }
        }
    }
}
