using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Infrastructure.Helpers
{
    public static class IpHelper
    {
        // TODO: Move everything bellow to IP helper
        public static bool IsIPV6(string IpAddress)
        {
            return IPAddress.TryParse(IpAddress, out IPAddress address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
        }
        public static bool IsIPV4(string IPAddress)
        {
            return Regex.IsMatch(IPAddress, @"^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");
        }
        public static bool IsValidIP(string IPAddress)
        {
            return IsIPV4(IPAddress) || IsIPV6(IPAddress);
        }
        public static uint IPAddressInt32(string IPAddress)
        {
            return IPAddressInt32(IPAddress.Split('.'));
        }
        public static uint IPAddressInt32(string[] Octets)
        {
            return Octets.
                Select((x, i) => uint.Parse(x) << (24 - 8 * i)).
                Aggregate((x, y) => x | y);
        }

        public static (long, long) IPAddressInt128(string IpAddress)
        {
            IPAddress address = IPAddress.Parse(IpAddress);
            // Reverse network byte order to host byte order
            byte[] bytes = address.GetAddressBytes().Reverse().ToArray();
            long low = BitConverter.ToInt64(bytes, 0);
            long high = BitConverter.ToInt64(bytes, 8);
            return (high, low);
        }

    }
}
