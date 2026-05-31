using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace LoanClaimsManagement.Data
{
    public class DbHelper
    {
        private readonly string _connectionString;

        public DbHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        public string ConnectionString => _connectionString;

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameter[] parameters)
        {
            using var connection = GetConnection();
            using var command = new SqlCommand(sql, connection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            await connection.OpenAsync();
            return await command.ExecuteNonQueryAsync();
        }

        public async Task<object?> ExecuteScalarAsync(string sql, params SqlParameter[] parameters)
        {
            using var connection = GetConnection();
            using var command = new SqlCommand(sql, connection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            await connection.OpenAsync();
            return await command.ExecuteScalarAsync();
        }

        public async Task ExecuteReaderAsync(string sql, Func<SqlDataReader, Task> readAction, params SqlParameter[] parameters)
        {
            using var connection = GetConnection();
            using var command = new SqlCommand(sql, connection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            await readAction(reader);
        }
    }
}
