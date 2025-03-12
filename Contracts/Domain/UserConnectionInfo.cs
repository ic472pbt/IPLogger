using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contracts.Domain
{
    public class UserConnectionInfo
    {
        public long UserId { get; set; }
        public int LastEventId { get; set; }
        public bool LastEventIsIPv6 { get; set; }
        public DateTimeOffset DateTime { get; set; }
        public required string IPAddress { get; set; }
    }
}
