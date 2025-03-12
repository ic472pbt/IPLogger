using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Postgres
{
    public class Postgres(ILogger logger, string connectionString)
    {
        private readonly string _connectionString = connectionString;
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
