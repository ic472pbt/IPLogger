using InboundEndpoint.Data;
using InboundEndpoint.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace InboundEndpoint.Infrastructure.Postgres.Models
{
    public class Session(IMemoryCache memoryCache)
    {
        private readonly IMemoryCache _memoryCache = memoryCache;

        public async ValueTask<int> GetNewSessionId(AppDbContext appDbContext, long UserId)
        {
            var cacheKey = $"SessionId_{UserId}";
            if (_memoryCache.TryGetValue(cacheKey, out int cachedSessionId))
            {
                return cachedSessionId;
            }

            var nextSessionId =
                (await appDbContext.
                    Sessions.
                    Where(s => s.UserId == UserId).
                    MaxAsync(s => s.SessionId)) + 1;
            var session = new SessionEntity()
            {
                UserId = UserId,
                SessionId = nextSessionId,
                LastCommitId = 0
            };
            appDbContext.Sessions.Add(session);
            await appDbContext.SaveChangesAsync();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromHours(24));

            _memoryCache.Set(cacheKey, session.SessionId, cacheEntryOptions);

            return session.SessionId;
        }
    }
}
