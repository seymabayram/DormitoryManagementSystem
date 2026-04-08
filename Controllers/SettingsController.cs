using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using System.Security.Claims;

namespace DormitoryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class SettingsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SettingsController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new SettingsViewModel();
            bool isAdmin = User.IsInRole("Admin");

            if (isAdmin)
            {
                // Load Global Settings
                var dict = await _context.SystemSettings.ToDictionaryAsync(s => s.KeyName, s => s.Value);
                viewModel.GlobalSettings.DormitoryName = dict.GetValueOrDefault("DormitoryName", "My Dorm");
                viewModel.GlobalSettings.DormitoryAddress = dict.GetValueOrDefault("DormitoryAddress", "");
                viewModel.GlobalSettings.ContactPhone = dict.GetValueOrDefault("ContactPhone", "");
                viewModel.GlobalSettings.ContactEmail = dict.GetValueOrDefault("ContactEmail", "");
                viewModel.GlobalSettings.DefaultMonthlyDue = decimal.TryParse(dict.GetValueOrDefault("DefaultMonthlyDue", "0"), out var d) ? d : 0;
                viewModel.GlobalSettings.LatePenaltyFee = decimal.TryParse(dict.GetValueOrDefault("LatePenaltyFee", "0"), out var p) ? p : 0;

                // Load Staff List
                viewModel.StaffList = await _context.Staffs.Include(s => s.User).ToListAsync();

                // Load Audit Logs (last 100)
                viewModel.RecentLogs = await _context.AuditLogs
                    .Include(a => a.User)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(100)
                    .ToListAsync();
            }

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGlobalSettings(SettingsViewModel model)
        {
            // Clear validation errors from sub-models not relevant to this form
            ModelState.Remove("ProfileSettings.CurrentPassword");
            ModelState.Remove("ProfileSettings.NewPassword");
            ModelState.Remove("ProfileSettings.ConfirmPassword");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields correctly.";
                return RedirectToAction(nameof(Index));
            }

            var keys = new Dictionary<string, string>
            {
                { "DormitoryName",   model.GlobalSettings.DormitoryName   ?? string.Empty },
                { "DormitoryAddress",model.GlobalSettings.DormitoryAddress ?? string.Empty },
                { "ContactPhone",    model.GlobalSettings.ContactPhone     ?? string.Empty },
                { "ContactEmail",    model.GlobalSettings.ContactEmail     ?? string.Empty },
                { "DefaultMonthlyDue", model.GlobalSettings.DefaultMonthlyDue.ToString() },
                { "LatePenaltyFee",    model.GlobalSettings.LatePenaltyFee.ToString()    }
            };

            foreach (var kvp in keys)
            {
                var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.KeyName == kvp.Key);
                if (setting == null)
                {
                    _context.SystemSettings.Add(new SystemSetting { KeyName = kvp.Key, Value = kvp.Value });
                }
                else
                {
                    setting.Value = kvp.Value;
                    _context.SystemSettings.Update(setting);
                }
            }

            await LogAction("Updated Global Settings");
            await _context.SaveChangesAsync();
            TempData["Success"] = "Global settings updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(SettingsViewModel model)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    if (user.PasswordHash != model.ProfileSettings.CurrentPassword)
                    {
                        TempData["Error"] = "Current password is incorrect.";
                        return RedirectToAction(nameof(Index));
                    }

                    user.PasswordHash = model.ProfileSettings.NewPassword;
                    _context.Users.Update(user);
                    await LogAction("Updated their password");
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Password updated successfully.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(string Name, string Surname, string Email, string Username, string Password)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Staff");
            if (role != null)
            {
                if (await _context.Users.AnyAsync(u => u.Username == Username))
                {
                    TempData["Error"] = "Username already exists.";
                    return RedirectToAction(nameof(Index));
                }

                var user = new User { Username = Username, PasswordHash = Password, RoleId = role.Id, IsActive = true };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var staff = new Staff { Name = Name, Surname = Surname, Email = Email, UserId = user.Id };
                _context.Staffs.Add(staff);
                await LogAction($"Created new staff: {Username}");
                await _context.SaveChangesAsync();
                TempData["Success"] = "Staff account created successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                // Prevent admin from disabling themselves
                var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (currentUserIdStr == userId.ToString())
                {
                    TempData["Error"] = "You cannot deactivate your own account.";
                    return RedirectToAction(nameof(Index));
                }

                user.IsActive = !user.IsActive;
                _context.Users.Update(user);
                await LogAction($"Toggled user status for ID: {userId}. New status: {(user.IsActive ? "Active" : "Inactive")}");
                await _context.SaveChangesAsync();
                TempData["Success"] = $"User account is now {(user.IsActive ? "Active" : "Inactive")}.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DownloadBackup()
        {
            string dbPath = Path.Combine(_env.ContentRootPath, "DormitoryV2.db");
            if (!System.IO.File.Exists(dbPath))
            {
                TempData["Error"] = "Database file not found.";
                return RedirectToAction(nameof(Index));
            }

            await LogAction("Downloaded a database backup");
            await _context.SaveChangesAsync();

            var bytes = await System.IO.File.ReadAllBytesAsync(dbPath);
            return File(bytes, "application/octet-stream", $"DormitoryBackup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        }

        private async Task LogAction(string description)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    ActionDesc = description,
                    Timestamp = DateTime.Now
                });
            }
        }
    }
}
