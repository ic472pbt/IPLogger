namespace ProjectTests
{
    using System;
    using System.Globalization;
    using NUnit.Framework;
    using Infrastructure.Helpers;

    namespace ProjectTests
    {
        [TestFixture]
        public class IpHelperTests
        {
            [TestCase("2001:0db8:85a3:0000:0000:8a2e:0370:7334", true)]
            [TestCase("2001:db8:85a3::8a2e:370:7334", true)]
            [TestCase("::1", true)]
            [TestCase("192.168.1.1", false)]
            [TestCase("invalid_ip", false)]
            public void IsIPV6_ShouldValidateCorrectly(string ipAddress, bool expected)
            {
                var result = IpHelper.IsIPV6(ipAddress);
                Assert.That(result, Is.EqualTo(expected));
            }

            [TestCase("192.168.1.1", true)]
            [TestCase("255.255.255.255", true)]
            [TestCase("0.0.0.0", true)]
            [TestCase("2001:0db8:85a3:0000:0000:8a2e:0370:7334", false)]
            [TestCase("invalid_ip", false)]
            public void IsIPV4_ShouldValidateCorrectly(string ipAddress, bool expected)
            {
                var result = IpHelper.IsIPV4(ipAddress);
                Assert.AreEqual(expected, result);
            }

            [TestCase("192.168.1.1", true)]
            [TestCase("2001:0db8:85a3:0000:0000:8a2e:0370:7334", true)]
            [TestCase("invalid_ip", false)]
            public void IsValidIP_ShouldValidateCorrectly(string ipAddress, bool expected)
            {
                var result = IpHelper.IsValidIP(ipAddress);
                Assert.AreEqual(expected, result);
            }

            [TestCase("192.168.1.1", 3232235777)]
            [TestCase("0.0.0.0", 0U)]
            [TestCase("255.255.255.255", 4294967295)]
            public void IPAddressInt32_ShouldConvertCorrectly(string ipAddress, uint expected)
            {
                var result = IpHelper.IPAddressInt32(ipAddress);
                Assert.AreEqual(expected, result);
            }

            [TestCase(new string[] { "192", "168", "1", "1" }, 3232235777)]
            [TestCase(new string[] { "0", "0", "0", "0" }, 0U)]
            [TestCase(new string[] { "255", "255", "255", "255" }, 4294967295)]
            public void IPAddressInt32_WithOctets_ShouldConvertCorrectly(string[] octets, uint expected)
            {
                var result = IpHelper.IPAddressInt32(octets);
                Assert.AreEqual(expected, result);
            }

            [TestCase("2001:0db8:85a3:0000:0000:8a2e:0370:7334", 2306139570357600256, 151930230829876)]
            [TestCase("::1", 0, 1)]
            public void IPAddressInt128_ShouldConvertCorrectly(string ipAddress, long expectedHigh, long expectedLow)
            {
                var (high, low) = IpHelper.IPAddressInt128(ipAddress);
                Assert.AreEqual(expectedHigh, high);
                Assert.AreEqual(expectedLow, low);
            }
        }
    }

}