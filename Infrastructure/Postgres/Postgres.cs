using Microsoft.Extensions.Logging;
using Npgsql;
using Contracts.Domain;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Numerics;
using Infrastructure.Helpers;

namespace Infrastructure.Postgres
{
    public class Postgres(ILogger logger, string connectionString)
    {
        private readonly string _connectionString = connectionString;
        /// <summary>
        /// Get the latest connection info for the user. Cached version queried from the Users table.
        /// </summary>
        /// <param name="UserId">User id</param>
        /// <returns></returns>
        public async Task<UserConnectionInfo?> GetUserConnectionInfo(long UserId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            string sql =
               @"SELECT 
                    U.""LastEventId"", 
                    U.""LastEventIsIPv6"",
                    CASE 
                        WHEN U.""LastEventIsIPv6"" THEN C6.""ConnectionTime"" 
                        ELSE C4.""ConnectionTime"" 
                    END AS ConnectionTime,
                    CASE 
                        WHEN U.""LastEventIsIPv6"" THEN C6.""IpAddressLow""
                        ELSE C4.""IpAddress"" 
                    END AS IpAddress,
                    CASE 
                        WHEN U.""LastEventIsIPv6"" THEN C6.""IpAddressHigh""
                        ELSE 0 
                    END AS IpAddressHigh
                FROM ""Users"" U
                LEFT JOIN ""ConnectionsV4"" C4 ON U.""LastConnectionId"" = C4.""ConnectionDataId"" AND NOT U.""LastEventIsIPv6""
                LEFT JOIN ""ConnectionsV6"" C6 ON U.""LastConnectionId"" = C6.""ConnectionDataId"" AND U.""LastEventIsIPv6""
                WHERE U.""UserId"" = @UserId";
            using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", UserId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                bool isIPv6 = reader.GetBoolean(1);
                byte[] ipAddressBytes;
                if (isIPv6)
                {
                    var ipAddressLow = ((BigInteger)reader.GetFieldValue<long>(3)).ToByteArray(isBigEndian:true);
                    var ipAddressHigh = ((BigInteger)reader.GetFieldValue<long>(4)).ToByteArray(isBigEndian: true);
                    ipAddressBytes = new byte[ipAddressLow.Length + ipAddressHigh.Length];
                    Buffer.BlockCopy(ipAddressHigh, 0, ipAddressBytes, 0, ipAddressHigh.Length);
                    Buffer.BlockCopy(ipAddressLow, 0, ipAddressBytes, ipAddressHigh.Length, ipAddressLow.Length);
                }
                else
                {
                    ipAddressBytes = ((BigInteger)reader.GetFieldValue<long>(3)).ToByteArray(isBigEndian: true);
                }
                return new UserConnectionInfo
                {
                    UserId = UserId,
                    LastEventId = reader.GetInt32(0),
                    LastEventIsIPv6 = isIPv6,
                    DateTime = reader.GetDateTime(2).ToLocalTime(),
                    IPAddress = new IPAddress(ipAddressBytes).ToString()
                };
            }
            return null;
        }

        private static string BuildIpV4Condition(string prefix)
        {
            var octets = prefix.Split('.');
            // point query if the prefix is a full IP address
            if (octets.Length == 4 && octets.Last().Length == 3)
            {
                return $"C4.\"IpAddress\" = {IpHelper.IPAddressInt32(octets)}";
            }
            else
            {
                switch (octets.Last().Length)
                {
                    case 1:
                        int lastOctet1 = int.Parse(octets[^1]);
                        uint ipFrom5 = IpHelper.IPAddressInt32(octets);
                        octets[^1] = (lastOctet1 + 1).ToString();
                        uint ipTo5 = IpHelper.IPAddressInt32(octets) - 1;

                        octets[^1] = (lastOctet1 * 10).ToString();
                        uint ipFrom4 = IpHelper.IPAddressInt32(octets);
                        octets[^1] = ((lastOctet1 + 1) * 10).ToString();
                        uint ipTo4 =  IpHelper.IPAddressInt32(octets) - 1;

                        octets[^1] = (lastOctet1 * 100).ToString();
                        uint ipFrom1 = IpHelper.IPAddressInt32(octets);
                        octets[^1] = lastOctet1 == 2 ? "255" : ((lastOctet1 + 1) * 100).ToString();
                        uint ipTo1 = lastOctet1 == 2 ? IpHelper.IPAddressInt32(octets)  : IpHelper.IPAddressInt32(octets) - 1;
                        return $"C4.\"IpAddress\" BETWEEN {ipFrom1} AND {ipTo1} OR C4.\"IpAddress\" BETWEEN {ipFrom4} AND {ipTo4} OR C4.\"IpAddress\" BETWEEN {ipFrom5} AND {ipTo5}";
                    case 2:
                        int lastOctet2 = int.Parse(octets[^1]);
                        uint ipFrom3 = IpHelper.IPAddressInt32(octets);
                        octets[^1] = (lastOctet2 + 1).ToString();
                        uint ipTo3 = IpHelper.IPAddressInt32(octets) - 1;

                        octets[^1] = (lastOctet2 * 10).ToString();
                        uint ipFrom2 = IpHelper.IPAddressInt32(octets);
                        octets[^1] = lastOctet2 == 25 ? "255" : ((lastOctet2 + 1) * 10).ToString();
                        uint ipTo2 = lastOctet2 == 25 ? IpHelper.IPAddressInt32(octets)  : IpHelper.IPAddressInt32(octets) - 1;
                        return $"C4.\"IpAddress\" BETWEEN {ipFrom2} AND {ipTo2} OR C4.\"IpAddress\" BETWEEN {ipFrom3} AND {ipTo3}";
                    default:
                        uint ip = IpHelper.IPAddressInt32(octets);
                        return $"C4.\"IpAddress\" >= {ip}";
                }
            }
        }

        private static string BuildIpV6Condition(string prefix) => throw new NotImplementedException("Same idea as for IPV4");

        public async Task<List<UserIpInfo>> FindUsersByIpPrefix(string prefix)
        {
            bool isIPv6 = prefix.Contains(':');
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            string condition = isIPv6
                ? BuildIpV6Condition(prefix)
                : BuildIpV4Condition(prefix);
            string sql =
                isIPv6
                ? @$"SELECT 
                        U.""UserId"",
                        C6.""IpAddressLow"",
                        C6.""IpAddressHigh""
                    FROM ""ConnectionsV6"" C6 
                    LEFT JOIN ""Users"" U ON U.""UserId"" = C6.""UserId""
                    WHERE {condition}"
                : @$"SELECT DISTINCT
                        U.""UserId"",
                        C4.""IpAddress""
                    FROM ""ConnectionsV4"" C4 
                    LEFT JOIN ""Users"" U ON U.""UserId"" = C4.""UserId""
                    WHERE {condition}";
            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();
            var users = new List<UserIpInfo>();
            while (reader.Read())
            {
                // TODO: Implement IpV6 to string algorithm
                users.Add(new UserIpInfo
                {
                    UserId = reader.GetInt64(0),
                    IpAddress = new IPAddress(((BigInteger)reader.GetFieldValue<long>(1)).ToByteArray(isBigEndian:true)).ToString()
                });
            }
            return users;
        }

        public async Task<List<string>> GetAllV4IpsForUser(long userId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            string sql =
                @"SELECT DISTINCT
                    C4.""IpAddress""
                FROM ""ConnectionsV4"" C4 
                WHERE C4.""UserId"" = @UserId";
            using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            using var reader = await cmd.ExecuteReaderAsync();
            var ips = new List<string>();
            while (reader.Read())
            {
                ips.Add(new IPAddress(((BigInteger)reader.GetFieldValue<long>(0)).ToByteArray(isBigEndian: true)).ToString());
            }
            return ips;
        }
        public async Task<List<string>> GetAllV6IpsForUser(long userId)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            string sql =
                @"SELECT DISTINCT
                    C6.""IpAddressLow"",
                    C6.""IpAddressHigh""
                FROM ""ConnectionsV6"" C6 
                WHERE C6.""UserId"" = @UserId";
            using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            using var reader = await cmd.ExecuteReaderAsync();
            var ips = new List<string>();
            while (reader.Read())
            {
                var ipAddressLow = ((BigInteger)reader.GetFieldValue<long>(0)).ToByteArray(isBigEndian: true);
                var ipAddressHigh = ((BigInteger)reader.GetFieldValue<long>(1)).ToByteArray(isBigEndian: true);
                var ipAddressBytes = new byte[ipAddressLow.Length + ipAddressHigh.Length];
                Buffer.BlockCopy(ipAddressHigh, 0, ipAddressBytes, 0, ipAddressHigh.Length);
                Buffer.BlockCopy(ipAddressLow, 0, ipAddressBytes, ipAddressHigh.Length, ipAddressLow.Length);
                ips.Add(new IPAddress(ipAddressBytes).ToString());
            }
            return ips;
        }

        public async Task Connect()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            logger.LogInformation("Connected to Postgres");
        }
        public async Task ExecuteNonQuery(string query)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var cmd = new NpgsqlCommand(query, connection);
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("Executed query: {Query}", query);
        }
        public async Task ExecuteQuery(string query)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var cmd = new NpgsqlCommand(query, connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                logger.LogInformation("Row: {Row}", reader.GetString(0));
            }
        }

        public async Task BatchInsert(string tableName, IEnumerable<Dictionary<string, object>> rows)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                foreach (var row in rows)
                {
                    var columns = string.Join(", ", row.Keys);
                    var values = string.Join(", ", row.Keys.Select((k, i) => $"@p{i}"));
                    var query = $"INSERT INTO {tableName} ({columns}) VALUES ({values})";
                    using var cmd = new NpgsqlCommand(query, connection, transaction);
                    for (int i = 0; i < row.Values.Count; i++)
                    {
                        cmd.Parameters.AddWithValue($"@p{i}", row.Values.ElementAt(i));
                    }
                    await cmd.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
                logger.LogInformation("Batch insert completed successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.LogError(ex, "Batch insert failed.");
                throw;
            }
        }
    }
}
