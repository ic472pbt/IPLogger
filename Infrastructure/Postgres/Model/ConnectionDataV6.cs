using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Postgres.Model
{
    [Index(nameof(IpAddressHigh), nameof(IpAddressLow))]
    public class ConnectionDataV6
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ConnectionDataId { get; set; }
        public long UserId { get; set; }
        public DateTimeOffset ConnectionTime { get; set; }
        public long IpAddressHigh { get; set; }
        public long IpAddressLow { get; set; }
        public UserData User { get; set; } = null!;
    }
}
