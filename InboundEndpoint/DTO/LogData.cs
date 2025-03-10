using System.Text.RegularExpressions;
using System.Numerics;
using System.Globalization;

namespace InboundEndpoint
{
    public class LogData
    {
        // Idempotency key
        public int EventId { get; set; }
        public long UserId { get; set; }
        public required string IPAddress { get; set; }

        // TODO: Move everything bellow to IP helper
        public bool IsIPV6()
        {
            return Regex.IsMatch(IPAddress, @"^([0-9a-fA-F]{1,4}:){7}([0-9a-fA-F]{1,4}|:)$");
        }
        public bool IsIPV4()
        {
            return Regex.IsMatch(IPAddress, @"^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");
        }
        public bool IsValidIP()
        {
            return IsIPV4() || IsIPV6();
        }
        public uint IPAddressInt32()
        {
            return IPAddress.
                Split('.').
                Select((x, i) => uint.Parse(x) << (24 - 8 * i)).
                Aggregate((x, y) => x | y);
        }
        public (long, long) IPAddressInt128()
        {
            var parts = IPAddress.Split(':');
            var high = parts.Take(4).Aggregate(0L, (acc, x) => (acc << 16) | ushort.Parse(x, NumberStyles.HexNumber));
            var low = parts.Skip(4).Aggregate(0L, (acc, x) => (acc << 16) | ushort.Parse(x, NumberStyles.HexNumber));
            return (high, low);
        }

    }
}
