using System.Text.RegularExpressions;
using System.Globalization;

namespace Contracts.Domain
{
    public class LogData
    {
        // Idempotency key
        public int EventId { get; set; }
        public long UserId { get; set; }
        public required string IPAddress { get; set; }        
    }
}
