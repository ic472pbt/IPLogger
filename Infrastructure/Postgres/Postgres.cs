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
            if (octets.Length == 4 && octets.Last().Length == 3)
            {
                return $"C4.\"IpAddress\" = {IpHelper.IPAddressInt32(octets)}";
            }
            else
            {
                return $"C4.\"IpAddress\"::text LIKE '{prefix}.%'";
            }
        }

        public async Task<List<UserIpInfo>> FindUsersByIpPrefix(string prefix)
        {
            bool isIPv6 = prefix.Contains(':');
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            string condition = isIPv6
                ? $"C6.\"IpAddressLow\"::text LIKE '{prefix}%' OR C6.\"IpAddressHigh\"::text LIKE '{prefix}%'"
                : BuildIpV4Condition(prefix);
            string sql =
                isIPv6
                ? @$"SELECT 
                        U.""UserId""
                    FROM ""ConnectionsV6"" C6 
                    LEFT JOIN ""Users"" U ON U.""UserId"" = C6.""UserId""
                    WHERE {condition}"
                : @$"SELECT 
                        U.""UserId""
                    FROM ""ConnectionsV4"" C4 
                    LEFT JOIN ""Users"" U ON U.""UserId"" = C4.""UserId""
                    WHERE {condition}";
            using var cmd = new NpgsqlCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync();
            var users = new List<UserIpInfo>();
            while (reader.Read())
            {
                users.Add(new UserIpInfo
                {
                    UserId = reader.GetInt64(0),
                    IpAddress = new IPAddress(((BigInteger)reader.GetFieldValue<long>(1)).ToByteArray()).ToString()
                });
            }
            return users;
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
