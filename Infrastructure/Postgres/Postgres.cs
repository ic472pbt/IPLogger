using Microsoft.Extensions.Logging;
using Npgsql;
using Contracts.Domain;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Numerics;

namespace Infrastructure.Postgres
{
    public class Postgres(ILogger logger, string connectionString)
    {
        private readonly string _connectionString = connectionString;

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
                    ipAddressBytes = ((BigInteger)reader.GetFieldValue<long>(3)).ToByteArray();
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
