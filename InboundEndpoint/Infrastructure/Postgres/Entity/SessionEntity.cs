using System.ComponentModel.DataAnnotations;

namespace InboundEndpoint.Entity
{
    public class SessionEntity
    {
        [Key]
        public int SessionId { get; set; }
        public long UserId { get; set; }
        public int LastCommitId { get; set; }
    }
}
