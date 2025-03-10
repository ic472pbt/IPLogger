using InboundEndpoint.Context;
using InboundEndpoint.Controllers;
using InboundEndpoint.DTO;
using InboundEndpoint.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InboundEndpoint.Repository
{
    public class UserEntity(ILogger<LogController> logger, AppDbContext appDbContext)
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
    }
}
