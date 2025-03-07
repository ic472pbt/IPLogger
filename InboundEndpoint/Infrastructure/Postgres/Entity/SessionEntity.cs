using System.ComponentModel.DataAnnotations;

namespace InboundEndpoint.Infrastructure.Postgres.Entity
{
    public class SessionEntity
    {
        [Key]
        public int SessionId { get; set; }
        public long UserId { get; set; }
        public int LastCommitId { get; set; }
    }
}
