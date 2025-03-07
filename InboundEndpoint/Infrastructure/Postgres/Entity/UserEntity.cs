namespace InboundEndpoint.Infrastructure.Postgres.Entity
{
    public class UserEntity
    {
        public long UserId { get; set; }
        public required IEnumerable<ConnectionEntity> Connections { get; set; }
    }
}
