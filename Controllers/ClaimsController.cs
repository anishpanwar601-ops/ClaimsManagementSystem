using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using LoanClaimsManagement.Data;
using LoanClaimsManagement.Models;

namespace LoanClaimsManagement.Controllers
{
    [Authorize]
    public class ClaimsController : Controller
    {
        private readonly DbHelper _db;
        private readonly ILogger<ClaimsController> _logger;

        public ClaimsController(DbHelper db, ILogger<ClaimsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ================= CUSTOMER ACTIONS =================

        [Authorize(Roles = "Customer")]
        [HttpGet]
        public IActionResult Submit()
        {
            return View(new ClaimViewModel());
        }

        [Authorize(Roles = "Customer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(ClaimViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Get authenticated user ID
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Challenge();
                }
                int submittedByUserId = int.Parse(userIdClaim.Value);

                string sql = @"
                    INSERT INTO Claims (LoanAccountNumber, ClaimType, Description, DateSubmitted, StatusID, SubmittedByUserID)
                    VALUES (@LoanAccountNumber, @ClaimType, @Description, @DateSubmitted, @StatusID, @SubmittedByUserID)";

                SqlParameter[] parameters = {
                    new SqlParameter("@LoanAccountNumber", model.LoanAccountNumber),
                    new SqlParameter("@ClaimType", model.ClaimType),
                    new SqlParameter("@Description", model.Description),
                    new SqlParameter("@DateSubmitted", DateTime.Now),
                    new SqlParameter("@StatusID", 1), // 1 = Pending
                    new SqlParameter("@SubmittedByUserID", submittedByUserId)
                };

                await _db.ExecuteNonQueryAsync(sql, parameters);
                TempData["SuccessMessage"] = "Your claim has been submitted successfully!";
                return RedirectToAction(nameof(List));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting claim for Loan Account {LoanAccount}", model.LoanAccountNumber);
                ModelState.AddModelError(string.Empty, "An error occurred while saving the claim. Please try again.");
                return View(model);
            }
        }

        [Authorize(Roles = "Customer")]
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Challenge();
            }
            int userId = int.Parse(userIdClaim.Value);

            string sql = @"
                SELECT c.ClaimID, c.LoanAccountNumber, c.ClaimType, c.Description, c.DateSubmitted, 
                       c.StatusID, cs.StatusName, c.SubmittedByUserID, u.FullName AS SubmittedByFullName, 
                       c.OfficerComments, r.FullName AS ReviewedByFullName, c.ReviewDate
                FROM Claims c
                INNER JOIN ClaimStatuses cs ON c.StatusID = cs.StatusID
                INNER JOIN Users u ON c.SubmittedByUserID = u.UserID
                LEFT JOIN Users r ON c.ReviewedByUserID = r.UserID
                WHERE c.SubmittedByUserID = @UserID
                ORDER BY c.DateSubmitted DESC";

            var claims = new List<ClaimViewModel>();

            try
            {
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
                }, new SqlParameter("@UserID", userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching claims for user ID {UserID}", userId);
                ViewBag.ErrorMessage = "Failed to load claims database.";
            }

            return View(claims);
        }

        // ================= LOAN OFFICER ACTIONS =================

        [Authorize(Roles = "LoanOfficer")]
        [HttpGet]
        public async Task<IActionResult> Review(string? searchAccount, int? statusFilter, string? typeFilter)
        {
            // Populate Dropdowns
            ViewBag.StatusFilter = statusFilter;
            ViewBag.TypeFilter = typeFilter;
            ViewBag.SearchAccount = searchAccount;

            var claims = new List<ClaimViewModel>();

            string sql = @"
                SELECT c.ClaimID, c.LoanAccountNumber, c.ClaimType, c.Description, c.DateSubmitted, 
                       c.StatusID, cs.StatusName, c.SubmittedByUserID, u.FullName AS SubmittedByFullName, 
                       c.OfficerComments, r.FullName AS ReviewedByFullName, c.ReviewDate
                FROM Claims c
                INNER JOIN ClaimStatuses cs ON c.StatusID = cs.StatusID
                INNER JOIN Users u ON c.SubmittedByUserID = u.UserID
                LEFT JOIN Users r ON c.ReviewedByUserID = r.UserID
                WHERE (@SearchAccount IS NULL OR c.LoanAccountNumber LIKE '%' + @SearchAccount + '%')
                  AND (@StatusFilter IS NULL OR c.StatusID = @StatusFilter)
                  AND (@TypeFilter IS NULL OR c.ClaimType = @TypeFilter)
                ORDER BY c.StatusID ASC, c.DateSubmitted DESC";

            try
            {
                SqlParameter[] parameters = {
                    new SqlParameter("@SearchAccount", string.IsNullOrEmpty(searchAccount) ? (object)DBNull.Value : searchAccount),
                    new SqlParameter("@StatusFilter", statusFilter.HasValue ? (object)statusFilter.Value : DBNull.Value),
                    new SqlParameter("@TypeFilter", string.IsNullOrEmpty(typeFilter) ? (object)DBNull.Value : typeFilter)
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching claims for review");
                ViewBag.ErrorMessage = "Failed to load claims database.";
            }

            return View(claims);
        }

        [Authorize(Roles = "LoanOfficer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int claimId, int statusId, string? officerComments)
        {
            if (statusId != 2 && statusId != 3) // 2 = Approved, 3 = Rejected
            {
                TempData["ErrorMessage"] = "Invalid status update requested.";
                return RedirectToAction(nameof(Review));
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Challenge();
            }
            int officerUserId = int.Parse(userIdClaim.Value);

            string sql = @"
                UPDATE Claims
                SET StatusID = @StatusID,
                    ReviewedByUserID = @ReviewedByUserID,
                    ReviewDate = @ReviewDate,
                    OfficerComments = @OfficerComments
                WHERE ClaimID = @ClaimID";

            try
            {
                SqlParameter[] parameters = {
                    new SqlParameter("@StatusID", statusId),
                    new SqlParameter("@ReviewedByUserID", officerUserId),
                    new SqlParameter("@ReviewDate", DateTime.Now),
                    new SqlParameter("@OfficerComments", string.IsNullOrEmpty(officerComments) ? (object)DBNull.Value : officerComments),
                    new SqlParameter("@ClaimID", claimId)
                };

                int rowsAffected = await _db.ExecuteNonQueryAsync(sql, parameters);

                if (rowsAffected > 0)
                {
                    string statusText = statusId == 2 ? "Approved" : "Rejected";
                    TempData["SuccessMessage"] = $"Claim #{claimId} has been successfully {statusText}!";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Claim #{claimId} could not be found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for Claim ID {ClaimID} by officer {OfficerID}", claimId, officerUserId);
                TempData["ErrorMessage"] = "Database error occurred while updating the claim status.";
            }

            return RedirectToAction(nameof(Review));
        }
    }
}
