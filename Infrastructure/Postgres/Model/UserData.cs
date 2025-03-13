using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Postgres.Model
{
    public class UserData
    {
        [Key]
        public long UserId { get; set; }
        public int LastEventId { get; set; }
        public ICollection<ConnectionDataV4> ConnectionsV4 { get; } = [];
        public ICollection<ConnectionDataV6> ConnectionsV6 { get; } = [];
        public bool LastEventIsIPv6 { get; internal set; }
        public long LastConnectionId { get; set; }

    }
}
