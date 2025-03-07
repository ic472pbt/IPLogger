using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace InboundEndpoint.Infrastructure.Postgres.Entity
{
    [Index(nameof(Octet1), nameof(Octet2), nameof(Octet3), nameof(Octet4))]
    public class IPEntity
    {
        [Key]
        public uint IpId { get; set; }

        [Required, MaxLength(4)]
        public required string Octet1 { get; set; }

        [Required, MaxLength(4)]
        public required string Octet2 { get; set; }

        [Required, MaxLength(4)]
        public required string Octet3 { get; set; }

        [Required, MaxLength(4)]
        public required string Octet4 { get; set; }

        public required ConnectionEntity Connection { get; set; }
    }
}
