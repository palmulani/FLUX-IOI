using System.Security.Claims;
using System.Text.RegularExpressions;
using Brightloop.Data;
using Brightloop.Models;
using Brightloop.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public AccountController(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Customer")) return RedirectToAction("Index", "Portal");
                return RedirectToAction("Index", "Dashboard");
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Please enter both email and password.";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower());
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            if (!user.IsActive)
            {
                ViewBag.Error = "Your account has been deactivated. Please contact support.";
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("CompanyName", user.CompanyName ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (user.Role == "Customer")
            {
                return RedirectToAction("Index", "Portal");
            }

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string fullName, string email, string password, string confirmPassword, string? companyName, string? phone)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "All required fields must be filled.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return View();
            }

            // Password Policy: More than 8 characters, with an uppercase letter, a lowercase letter and a special character
            if (password.Length <= 8)
            {
                ViewBag.Error = "Password must be more than 8 characters long (at least 9 characters).";
                return View();
            }

            if (!password.Any(char.IsUpper))
            {
                ViewBag.Error = "Password must contain at least one uppercase letter.";
                return View();
            }

            if (!password.Any(char.IsLower))
            {
                ViewBag.Error = "Password must contain at least one lowercase letter.";
                return View();
            }

            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                ViewBag.Error = "Password must contain at least one special character (e.g. !@#$%^&*).";
                return View();
            }

            // Unique Email Check
            var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == email.Trim().ToLower());
            if (exists)
            {
                ViewBag.Error = "An account with this email address already exists.";
                return View();
            }

            var newUser = new User
            {
                FullName = fullName.Trim(),
                Email = email.Trim().ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "Customer", // Always customer upon public signup
                CompanyName = companyName?.Trim(),
                Phone = phone?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Auto Login
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, newUser.Id.ToString()),
                new Claim(ClaimTypes.Name, newUser.FullName),
                new Claim(ClaimTypes.Email, newUser.Email),
                new Claim(ClaimTypes.Role, newUser.Role),
                new Claim("CompanyName", newUser.CompanyName ?? "")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return RedirectToAction("Index", "Portal");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Please enter your email.";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower());
            if (user != null)
            {
                var token = Guid.NewGuid().ToString("N");
                user.ResetToken = token;
                user.ResetTokenExpiry = DateTime.UtcNow.AddHours(2);
                await _context.SaveChangesAsync();

                var resetLink = Url.Action("ResetPassword", "Account", new { token = token, email = user.Email }, Request.Scheme)
                                ?? $"http://localhost:5000/Account/ResetPassword?token={token}&email={user.Email}";

                await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);
                ViewBag.Success = "Password reset instructions have been sent to your email address.";
                ViewBag.DemoResetLink = resetLink; // Also displayed on UI for easy testing!
            }
            else
            {
                // Security practice: Don't reveal whether email exists, or state sent
                ViewBag.Success = "If this email is registered, password reset instructions have been sent.";
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token, string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.ResetToken == token);
            if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                ViewBag.Error = "This password reset link is invalid or has expired.";
                return View("ErrorReset");
            }

            ViewBag.Token = token;
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string token, string email, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                ViewBag.Token = token;
                ViewBag.Email = email;
                return View();
            }

            if (newPassword.Length <= 8 || !newPassword.Any(char.IsUpper) || !newPassword.Any(char.IsLower) || !newPassword.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                ViewBag.Error = "Password must be > 8 characters with 1 uppercase, 1 lowercase, and 1 special character.";
                ViewBag.Token = token;
                ViewBag.Email = email;
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.ResetToken == token);
            if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                ViewBag.Error = "Reset token has expired or is invalid.";
                return View("ErrorReset");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your password has been successfully reset. Please log in.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // Staff Management - ADMIN ONLY
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ManageStaff()
        {
            var staffList = await _context.Users
                .Where(u => u.Role == "Staff" || u.Role == "Admin")
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return View(staffList);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStaff(string fullName, string email, string password, string role)
        {
            if (password.Length <= 8 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                TempData["Error"] = "Staff password must be >8 characters with uppercase, lowercase and special symbol.";
                return RedirectToAction("ManageStaff");
            }

            var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == email.Trim().ToLower());
            if (exists)
            {
                TempData["Error"] = "A user with this email already exists.";
                return RedirectToAction("ManageStaff");
            }

            var newStaff = new User
            {
                FullName = fullName.Trim(),
                Email = email.Trim().ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = (role == "Admin") ? "Admin" : "Staff",
                CompanyName = "Brightloop Software",
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newStaff);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Staff member '{newStaff.FullName}' added successfully.";
            return RedirectToAction("ManageStaff");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
