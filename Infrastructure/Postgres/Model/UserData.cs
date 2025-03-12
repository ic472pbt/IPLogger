using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Postgres.Model
{
    public class UserData
    {
        [Key]
        public long UserId { get; set; }
        /// Idempotence key to prevent duplicate messages
        public int LastEventId { get; set; }
        /// The last event was an IPv6 event
        public bool LastEventIsIPv6 { get; set; }
        /// Event link to the last event ether IPv4 or IPv6
        public long LastConnectionId { get; set; }
        public ICollection<ConnectionDataV4> ConnectionsV4 { get; } = [];
        public ICollection<ConnectionDataV6> ConnectionsV6 { get; } = [];
    }
}
