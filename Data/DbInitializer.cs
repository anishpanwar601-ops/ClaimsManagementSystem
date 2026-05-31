using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LoanClaimsManagement.Data
{
    public class DbInitializer
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DbInitializer> _logger;
        private static readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();

        public DbInitializer(IConfiguration configuration, IWebHostEnvironment env, ILogger<DbInitializer> logger)
        {
            _configuration = configuration;
            _env = env;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            string defaultConnectionString = _configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("DefaultConnection not found in configuration.");

            // Parse connection string to get DB name and master DB connection string
            var builder = new SqlConnectionStringBuilder(defaultConnectionString);
            string targetDatabaseName = builder.InitialCatalog;
            
            // Connect to master database first to check/create the target database
            builder.InitialCatalog = "master";
            string masterConnectionString = builder.ConnectionString;

            _logger.LogInformation("Checking if database {DbName} exists...", targetDatabaseName);

            try
            {
                using (var masterConn = new SqlConnection(masterConnectionString))
                {
                    await masterConn.OpenAsync();
                    
                    // Check if DB exists
                    string checkDbSql = $"SELECT database_id FROM sys.databases WHERE name = '{targetDatabaseName}'";
                    using (var checkCmd = new SqlCommand(checkDbSql, masterConn))
                    {
                        var result = await checkCmd.ExecuteScalarAsync();
                        if (result == null)
                        {
                            _logger.LogInformation("Database {DbName} does not exist. Creating it...", targetDatabaseName);
                            string createDbSql = $"CREATE DATABASE [{targetDatabaseName}]";
                            using (var createCmd = new SqlCommand(createDbSql, masterConn))
                            {
                                await createCmd.ExecuteNonQueryAsync();
                            }
                            _logger.LogInformation("Database {DbName} created successfully.", targetDatabaseName);
                        }
                    }
                }

                // Connect to the target database and run schema/seed scripts
                using (var conn = new SqlConnection(defaultConnectionString))
                {
                    await conn.OpenAsync();
                    _logger.LogInformation("Applying schema and seed scripts to {DbName}...", targetDatabaseName);

                    // Execute schema.sql
                    string schemaPath = Path.Combine(_env.ContentRootPath, "Database", "schema.sql");
                    if (File.Exists(schemaPath))
                    {
                        string schemaSql = await File.ReadAllTextAsync(schemaPath);
                        await ExecuteScriptAsync(conn, schemaSql);
                        _logger.LogInformation("schema.sql executed successfully.");
                    }
                    else
                    {
                        _logger.LogWarning("schema.sql not found at {Path}", schemaPath);
                    }

                    // Execute seed.sql
                    string seedPath = Path.Combine(_env.ContentRootPath, "Database", "seed.sql");
                    if (File.Exists(seedPath))
                    {
                        string seedSql = await File.ReadAllTextAsync(seedPath);
                        await ExecuteScriptAsync(conn, seedSql);
                        _logger.LogInformation("seed.sql executed successfully.");
                    }
                    else
                    {
                        _logger.LogWarning("seed.sql not found at {Path}", seedPath);
                    }

                    // Seed default users dynamically with hashed passwords if they don't exist
                    await SeedUsersAsync(conn);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initializing the database.");
                throw;
            }
        }

        private async Task ExecuteScriptAsync(SqlConnection conn, string script)
        {
            // Split script by GO statement if any, or run as a single batch
            // Note: Our scripts are standard SQL without GO command, but we clean them up
            string[] commands = script.Split(new[] { "GO\r\n", "GO\n", "go\r\n", "go\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var cmdText in commands)
            {
                if (string.IsNullOrWhiteSpace(cmdText)) continue;
                using (var cmd = new SqlCommand(cmdText, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task SeedUsersAsync(SqlConnection conn)
        {
            // Check if users exist
            string checkUsersSql = "SELECT COUNT(*) FROM Users";
            using (var countCmd = new SqlCommand(checkUsersSql, conn))
            {
                int count = (int)(await countCmd.ExecuteScalarAsync() ?? 0);
                if (count > 0)
                {
                    _logger.LogInformation("Database already contains users. Skipping user seeding.");
                    return;
                }
            }

            _logger.LogInformation("Seeding default users...");

            // Fetch Role IDs
            int adminRoleId = await GetRoleIdAsync(conn, "Admin");
            int officerRoleId = await GetRoleIdAsync(conn, "LoanOfficer");
            int customerRoleId = await GetRoleIdAsync(conn, "Customer");

            // Seed Admin User
            await InsertUserAsync(conn, "Anish", "Anish@123", adminRoleId, "Anish(System Administrator)", "Anish@claims.com");

            // Seed Loan Officers
            await InsertUserAsync(conn, "Samarth", "Samarth@123", officerRoleId, "Samarth(Officer)", "Samarth@claims.com");
            await InsertUserAsync(conn, "Naveen", "Naveen@123", officerRoleId, "Naveen(Officer)", "Naveen@claims.com");

            // Seed Customers
            await InsertUserAsync(conn, "Vivek", "Vivek@123", customerRoleId, "Vivek", "Vivek@gmail.com");
            await InsertUserAsync(conn, "Bhawesh", "Bhawesh@123", customerRoleId, "Bhawesh", "Bhawesh@yahoo.com");

            _logger.LogInformation("Users seeded successfully.");

            // Seed some sample claims if none exist
            string checkClaimsSql = "SELECT COUNT(*) FROM Claims";
            using (var countClaimsCmd = new SqlCommand(checkClaimsSql, conn))
            {
                int claimCount = (int)(await countClaimsCmd.ExecuteScalarAsync() ?? 0);
                if (claimCount == 0)
                {
                    _logger.LogInformation("Seeding sample claims...");
                    int cust1Id = await GetUserIdAsync(conn, "Vivek");
                    int cust2Id = await GetUserIdAsync(conn, "Bhawesh");

                    await InsertClaimAsync(conn, "L-908234", "Insurance Claim", "Vehicle loan claim following accident collision. Covered under policy auto-A9.", DateTime.Now.AddDays(-10), 1, cust1Id);
                    await InsertClaimAsync(conn, "L-112344", "Interest Waiver Request", "Requesting waiver of late interest fee due to banking app outage on payment date.", DateTime.Now.AddDays(-5), 2, cust1Id, "Approved by manager. Waiver processed.", await GetUserIdAsync(conn, "Samarth"));
                    await InsertClaimAsync(conn, "L-882341", "Dispute Charge", "Disputing administrative fee of $150 applied on account activation.", DateTime.Now.AddDays(-2), 1, cust2Id);
                }
            }
        }

        private async Task<int> GetRoleIdAsync(SqlConnection conn, string roleName)
        {
            string sql = "SELECT RoleID FROM Roles WHERE RoleName = @RoleName";
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@RoleName", roleName);
                return (int)(await cmd.ExecuteScalarAsync() ?? throw new Exception($"Role {roleName} not found in database."));
            }
        }

        private async Task<int> GetUserIdAsync(SqlConnection conn, string username)
        {
            string sql = "SELECT UserID FROM Users WHERE Username = @Username";
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Username", username);
                return (int)(await cmd.ExecuteScalarAsync() ?? throw new Exception($"User {username} not found."));
            }
        }

        private async Task InsertUserAsync(SqlConnection conn, string username, string password, int roleId, string fullName, string email)
        {
            string passwordHash = _passwordHasher.HashPassword(username, password);
            string sql = @"
                INSERT INTO Users (Username, PasswordHash, RoleID, FullName, Email) 
                VALUES (@Username, @PasswordHash, @RoleID, @FullName, @Email)";

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Username", username);
                cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                cmd.Parameters.AddWithValue("@RoleID", roleId);
                cmd.Parameters.AddWithValue("@FullName", fullName);
                cmd.Parameters.AddWithValue("@Email", email);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task InsertClaimAsync(SqlConnection conn, string loanAccount, string claimType, string desc, DateTime dateSub, int statusId, int subUserId, string? comments = null, int? revUserId = null)
        {
            string sql = @"
                INSERT INTO Claims (LoanAccountNumber, ClaimType, Description, DateSubmitted, StatusID, SubmittedByUserID, OfficerComments, ReviewedByUserID, ReviewDate) 
                VALUES (@LoanAccount, @ClaimType, @Desc, @DateSub, @StatusId, @SubUserId, @Comments, @RevUserId, @RevDate)";

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@LoanAccount", loanAccount);
                cmd.Parameters.AddWithValue("@ClaimType", claimType);
                cmd.Parameters.AddWithValue("@Desc", desc);
                cmd.Parameters.AddWithValue("@DateSub", dateSub);
                cmd.Parameters.AddWithValue("@StatusId", statusId);
                cmd.Parameters.AddWithValue("@SubUserId", subUserId);
                cmd.Parameters.AddWithValue("@Comments", (object?)comments ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RevUserId", (object?)revUserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RevDate", revUserId.HasValue ? DateTime.Now : DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
