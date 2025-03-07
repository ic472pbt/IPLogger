using Microsoft.EntityFrameworkCore;

namespace InboundEndpoint.Infrastructure.Postgres.Entity
{
    [Index(nameof( IpAddress))]
    public class ConnectionEntity
    {
        public long UserId { get; set; }
        public DateTime ConnectionTime { get; set; }
        public uint IpAddress { get; set; }
        public required UserEntity User { get; set; }
        public required IPEntity IPEntity { get; set; }
    }
}
