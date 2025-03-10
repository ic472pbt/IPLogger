using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InboundEndpoint.Model
{
    [Index(nameof(IpAddress))]
    public class ConnectionDataV4
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ConnectionDataId { get; set; }
        public long UserId { get; set; }
        public DateTimeOffset ConnectionTime { get; set; }
        public uint IpAddress { get; set; }
        public UserData User { get; set; } = null!;
    }
}
