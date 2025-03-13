using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contracts.Domain
{
    public class UserIpInfo
    {
        public long UserId { get; set; }
        public required string IpAddress { get; set; }
    }
}
