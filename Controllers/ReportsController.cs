using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using LoanClaimsManagement.Data;
using LoanClaimsManagement.Models;

namespace LoanClaimsManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        private readonly DbHelper _db;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(DbHelper db, ILogger<ReportsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(ReportViewModel model)
        {
            try
            {
                // 1. Populate Dropdowns (Statuses & Officers)
                await PopulateDropdownsAsync(model);

                // 2. Fetch Aggregated Metrics
                string metricsSql = @"
                    SELECT 
                        COUNT(*) as Total,
                        SUM(CASE WHEN StatusID = 1 THEN 1 ELSE 0 END) as Pending,
                        SUM(CASE WHEN StatusID = 2 THEN 1 ELSE 0 END) as Approved,
                        SUM(CASE WHEN StatusID = 3 THEN 1 ELSE 0 END) as Rejected
                    FROM Claims";

                await _db.ExecuteReaderAsync(metricsSql, async reader =>
                {
                    if (await reader.ReadAsync())
                    {
                        model.TotalClaimsCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        model.PendingCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        model.ApprovedCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        model.RejectedCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    }
                });

                // 3. Fetch Matching Claims based on filters
                model.MatchingClaims = await GetFilteredClaimsAsync(model.FilterStatusID, model.FilterDateFrom, model.FilterDateTo, model.FilterOfficerID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compiling dashboard reports.");
                ViewBag.ErrorMessage = "Failed to fetch database metrics.";
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportToCsv(int? statusId, DateTime? dateFrom, DateTime? dateTo, int? officerId)
        {
            try
            {
                var claims = await GetFilteredClaimsAsync(statusId, dateFrom, dateTo, officerId);

                var builder = new StringBuilder();
                // Write Header
                builder.AppendLine("Claim ID,Loan Account Number,Claim Type,Description,Date Submitted,Status,Submitted By,Reviewed By,Review Date,Officer Comments");

                foreach (var claim in claims)
                {
                    // Escape CSV fields to prevent issues with commas/quotes
                    string id = claim.ClaimID.ToString();
                    string acc = EscapeCsvField(claim.LoanAccountNumber);
                    string type = EscapeCsvField(claim.ClaimType);
                    string desc = EscapeCsvField(claim.Description);
                    string dateSub = claim.DateSubmitted.ToString("yyyy-MM-dd HH:mm:ss");
                    string status = EscapeCsvField(claim.StatusName);
                    string subBy = EscapeCsvField(claim.SubmittedByFullName);
                    string revBy = EscapeCsvField(claim.ReviewedByFullName ?? "");
                    string revDate = claim.ReviewDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                    string comments = EscapeCsvField(claim.OfficerComments ?? "");

                    builder.AppendLine($"{id},{acc},{type},{desc},{dateSub},{status},{subBy},{revBy},{revDate},{comments}");
                }

                byte[] fileBytes = Encoding.UTF8.GetBytes(builder.ToString());
                string fileName = $"ClaimsReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                return File(fileBytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting claims report to CSV.");
                TempData["ErrorMessage"] = "Failed to generate CSV export.";
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task PopulateDropdownsAsync(ReportViewModel model)
        {
            // Fetch Statuses
            string statusSql = "SELECT StatusID, StatusName FROM ClaimStatuses ORDER BY StatusID";
            await _db.ExecuteReaderAsync(statusSql, async reader =>
            {
                while (await reader.ReadAsync())
                {
                    model.Statuses.Add(new ClaimStatusItem
                    {
                        StatusID = reader.GetInt32(0),
                        StatusName = reader.GetString(1)
                    });
                }
            });

            // Fetch Officers
            string officerSql = @"
                SELECT u.UserID, u.FullName 
                FROM Users u
                INNER JOIN Roles r ON u.RoleID = r.RoleID
                WHERE r.RoleName = 'LoanOfficer'
                ORDER BY u.FullName";

            await _db.ExecuteReaderAsync(officerSql, async reader =>
            {
                while (await reader.ReadAsync())
                {
                    model.Officers.Add(new OfficerItem
                    {
                        UserID = reader.GetInt32(0),
                        FullName = reader.GetString(1)
                    });
                }
            });
        }

        private async Task<List<ClaimViewModel>> GetFilteredClaimsAsync(int? statusId, DateTime? dateFrom, DateTime? dateTo, int? officerId)
        {
            var claims = new List<ClaimViewModel>();

            string sql = @"
                SELECT c.ClaimID, c.LoanAccountNumber, c.ClaimType, c.Description, c.DateSubmitted, 
                       c.StatusID, cs.StatusName, c.SubmittedByUserID, u.FullName AS SubmittedByFullName, 
                       c.OfficerComments, r.FullName AS ReviewedByFullName, c.ReviewDate
                FROM Claims c
                INNER JOIN ClaimStatuses cs ON c.StatusID = cs.StatusID
                INNER JOIN Users u ON c.SubmittedByUserID = u.UserID
                LEFT JOIN Users r ON c.ReviewedByUserID = r.UserID
                WHERE (@StatusID IS NULL OR c.StatusID = @StatusID)
                  AND (@DateFrom IS NULL OR c.DateSubmitted >= @DateFrom)
                  AND (@DateTo IS NULL OR c.DateSubmitted <= @DateTo)
                  AND (@OfficerID IS NULL OR c.ReviewedByUserID = @OfficerID)
                ORDER BY c.DateSubmitted DESC";

            // If DateTo is provided, set it to the end of that day (23:59:59)
            DateTime? dateToParsed = dateTo?.Date.AddDays(1).AddSeconds(-1);

            SqlParameter[] parameters = {
                new SqlParameter("@StatusID", statusId.HasValue ? (object)statusId.Value : DBNull.Value),
                new SqlParameter("@DateFrom", dateFrom.HasValue ? (object)dateFrom.Value.Date : DBNull.Value),
                new SqlParameter("@DateTo", dateToParsed.HasValue ? (object)dateToParsed.Value : DBNull.Value),
                new SqlParameter("@OfficerID", officerId.HasValue ? (object)officerId.Value : DBNull.Value)
            };

            await _db.ExecuteReaderAsync(sql, async reader =>
            {
                while (await reader.ReadAsync())
                {
                    claims.Add(new ClaimViewModel
                    {
                        ClaimID = reader.GetInt32(0),
                        LoanAccountNumber = reader.GetString(1),
                        ClaimType = reader.GetString(2),
                        Description = reader.GetString(3),
                        DateSubmitted = reader.GetDateTime(4),
                        StatusID = reader.GetInt32(5),
                        StatusName = reader.GetString(6),
                        SubmittedByUserID = reader.GetInt32(7),
                        SubmittedByFullName = reader.GetString(8),
                        OfficerComments = reader.IsDBNull(9) ? null : reader.GetString(9),
                        ReviewedByFullName = reader.IsDBNull(10) ? null : reader.GetString(10),
                        ReviewDate = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
                    });
                }
            }, parameters);

            return claims;
        }

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }
    }
}
