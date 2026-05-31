using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using LoanClaimsManagement.Data;
using LoanClaimsManagement.Models;

namespace LoanClaimsManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly DbHelper _db;
        private readonly ILogger<AccountController> _logger;
        private static readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();

        public AccountController(DbHelper db, ILogger<AccountController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToDefaultRolePage();
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                string sql = @"
                    SELECT u.UserID, u.Username, u.PasswordHash, u.FullName, u.Email, r.RoleName
                    FROM Users u
                    INNER JOIN Roles r ON u.RoleID = r.RoleID
                    WHERE u.Username = @Username";

                int userId = 0;
                string username = "";
                string passwordHash = "";
                string fullName = "";
                string email = "";
                string roleName = "";
                bool userFound = false;

                await _db.ExecuteReaderAsync(sql, async reader =>
                {
                    if (await reader.ReadAsync())
                    {
                        userId = reader.GetInt32(0);
                        username = reader.GetString(1);
                        passwordHash = reader.GetString(2);
                        fullName = reader.GetString(3);
                        email = reader.GetString(4);
                        roleName = reader.GetString(5);
                        userFound = true;
                    }
                }, new SqlParameter("@Username", model.Username));

                if (!userFound)
                {
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return View(model);
                }

                // Verify Password Hash
                var verifyResult = _passwordHasher.VerifyHashedPassword(username, passwordHash, model.Password);
                if (verifyResult == PasswordVerificationResult.Failed)
                {
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return View(model);
                }

                // Create identity and sign in
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, roleName),
                    new Claim("FullName", fullName),
                    new Claim(ClaimTypes.Email, email)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation("User {Username} logged in with role {Role}.", username, roleName);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToDefaultRolePage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing login for user {Username}", model.Username);
                ModelState.AddModelError(string.Empty, "A database error occurred. Please contact administrator.");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToDefaultRolePage()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Reports");
            }
            else if (User.IsInRole("LoanOfficer"))
            {
                return RedirectToAction("Review", "Claims");
            }
            else if (User.IsInRole("Customer"))
            {
                return RedirectToAction("List", "Claims");
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
