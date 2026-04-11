using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using System.Security.Claims;

namespace DormitoryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff,Student")]
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
                var dict = await _context.SystemSettings.AsNoTracking().ToDictionaryAsync(s => s.KeyName, s => s.Value);
                viewModel.GlobalSettings.DormitoryName = dict.GetValueOrDefault("DormitoryName", "My Dorm");
                viewModel.GlobalSettings.DormitoryAddress = dict.GetValueOrDefault("DormitoryAddress", "");
                viewModel.GlobalSettings.ContactPhone = dict.GetValueOrDefault("ContactPhone", "");
                viewModel.GlobalSettings.ContactEmail = dict.GetValueOrDefault("ContactEmail", "");
                viewModel.GlobalSettings.DefaultMonthlyDue = decimal.TryParse(dict.GetValueOrDefault("DefaultMonthlyDue", "0"), out var d) ? d : 0;
                viewModel.GlobalSettings.LatePenaltyFee = decimal.TryParse(dict.GetValueOrDefault("LatePenaltyFee", "0"), out var p) ? p : 0;

                // Load User Lists (Admins & Staff)
                viewModel.AdminList = await _context.Admins.AsNoTracking().Include(a => a.User!).ThenInclude(u => u!.Role).ToListAsync();
                viewModel.StaffList = await _context.Staffs.AsNoTracking().Include(s => s.User!).ThenInclude(u => u!.Role).ToListAsync();

                // Load Audit Logs (last 100)
                viewModel.RecentLogs = await _context.AuditLogs
                    .AsNoTracking()
                    .Include(a => a.User)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(100)
                    .ToListAsync();
            }

            // If Student, load their specific info
            if (User.IsInRole("Student"))
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var student = await _context.Students.Include(s => s.Room).FirstOrDefaultAsync(s => s.UserId == userId);
                    if (student != null)
                    {
                        viewModel.StudentInfo = new StudentInfoViewModel
                        {
                            Id = student.Id,
                            Name = student.Name,
                            Surname = student.Surname,
                            Email = student.Email,
                            PhoneNumber = student.PhoneNumber,
                            RoomName = student.Room?.RoomNumber,
                            StudentId = student.StudentId
                        };
                    }
                }
            }
            else if (User.IsInRole("Staff"))
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var staff = await _context.Staffs.FirstOrDefaultAsync(s => s.UserId == userId);
                    if (staff != null)
                    {
                        viewModel.StudentInfo = new StudentInfoViewModel
                        {
                            Id = staff.Id,
                            Name = staff.Name,
                            Surname = staff.Surname,
                            Email = staff.Email,
                            PhoneNumber = staff.PhoneNumber
                        };
                    }
                }
            }

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGlobalSettings(SettingsViewModel model)
        {
            // Clear all validation errors except those for GlobalSettings
            foreach (var key in ModelState.Keys.Where(k => !k.StartsWith("GlobalSettings.")).ToList())
            {
                ModelState.Remove(key);
            }

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
            return Redirect(Url.Action("Index") + "#general");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(SettingsViewModel model)
        {
            if (model.ProfileSettings == null || 
                string.IsNullOrWhiteSpace(model.ProfileSettings.CurrentPassword) || 
                string.IsNullOrWhiteSpace(model.ProfileSettings.NewPassword) || 
                string.IsNullOrWhiteSpace(model.ProfileSettings.ConfirmPassword))
            {
                TempData["Error"] = "Please fill in all required fields.";
                return Redirect(Url.Action("Index") + "#profile");
            }

            if (model.ProfileSettings.NewPassword != model.ProfileSettings.ConfirmPassword)
            {
                TempData["Error"] = "New passwords do not match. Please ensure both fields are exactly the same.";
                return Redirect(Url.Action("Index") + "#profile");
            }

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    if (user.PasswordHash != model.ProfileSettings.CurrentPassword)
                    {
                        TempData["Error"] = "Current password is incorrect.";
                        return Redirect(Url.Action("Index") + "#profile");
                    }

                    user.PasswordHash = model.ProfileSettings.NewPassword;
                    _context.Users.Update(user);
                    await LogAction("Updated their password");
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Your password has been changed successfully.";
                }
            }
            return Redirect(Url.Action("Index") + "#profile");
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStudentInfo(SettingsViewModel model)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return RedirectToAction(nameof(Index));

            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
            if (student == null) return RedirectToAction(nameof(Index));

            var info = model.StudentInfo;
            List<string> changes = new();

            if (student.Name != info.Name) { student.Name = info.Name; changes.Add("First Name"); }
            if (student.Surname != info.Surname) { student.Surname = info.Surname; changes.Add("Last Name"); }
            if (student.Email != info.Email) { student.Email = info.Email; changes.Add("E-Mail"); }
            if (student.PhoneNumber != info.PhoneNumber) { student.PhoneNumber = info.PhoneNumber; changes.Add("Phone Number"); }

            if (changes.Count > 0)
            {
                _context.Students.Update(student);
                await LogAction($"Updated personal info: {string.Join(", ", changes)}");
                await _context.SaveChangesAsync();
                
                if (changes.Count == 1) 
                    TempData["Success"] = $"{changes[0]} has been updated successfully.";
                else 
                    TempData["Success"] = "Your profile information has been updated successfully.";
            }

            return Redirect(Url.Action("Index") + "#myinfo");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePersonnel(string Name, string Surname, string Email, string Username, string Password, string Role, string? PhoneNumber)
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Surname) || 
                string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Username) || 
                string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Role))
            {
                TempData["Error"] = "Please fill in all required fields correctly.";
                return Redirect(Url.Action("Index") + "#addstaff");
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == Role);
            if (role != null)
            {
                if (await _context.Users.AnyAsync(u => u.Username == Username))
                {
                    TempData["Error"] = "Username already exists.";
                    return Redirect(Url.Action("Index") + "#users");
                }

                var user = new User { Username = Username, PasswordHash = Password, RoleId = role.Id, IsActive = true };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                if (Role == "Admin")
                {
                    var admin = new Admin { Name = Name, Surname = Surname, Email = Email, UserId = user.Id };
                    _context.Admins.Add(admin);
                }
                else
                {
                    var staff = new Staff { Name = Name, Surname = Surname, Email = Email, UserId = user.Id, PhoneNumber = PhoneNumber };
                    _context.Staffs.Add(staff);
                }

                await LogAction($"Created new {Role}: {Username}");
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{Role} account created successfully.";
            }
            return Redirect(Url.Action("Index") + "#users");
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
                    return Redirect(Url.Action("Index") + "#users");
                }

                user.IsActive = !user.IsActive;
                _context.Users.Update(user);
                await LogAction($"Toggled user status for ID: {userId}. New status: {(user.IsActive ? "Active" : "Inactive")}");
                await _context.SaveChangesAsync();
                TempData["Success"] = $"User account is now {(user.IsActive ? "Active" : "Inactive")}.";
            }
            return Redirect(Url.Action("Index") + "#users");
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DownloadBackup()
        {
            string dbPath = Path.Combine(_env.ContentRootPath, "Dormitory.db");
            if (!System.IO.File.Exists(dbPath))
            {
                TempData["Error"] = "Database file not found.";
                return RedirectToAction(nameof(Index));
            }

            // Log the backup action
            await LogAction("Downloaded a database backup");
            await _context.SaveChangesAsync();

            // CRITICAL: Force a SQLite Checkpoint.
            // In WAL mode, SQLite writes to a separate -wal file. 
            // This command merges all changes from the -wal file back into the main .db file.
            await _context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");

            // Since the SQLite database is potentially locked by the application, we open it with FileShare.ReadWrite
            byte[] bytes;
            await using (var fs = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytes = new byte[fs.Length];
                await fs.ReadAsync(bytes, 0, bytes.Length);
            }
            return File(bytes, "application/octet-stream", $"DormitoryBackup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        }

        private Task LogAction(string description)
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
            return Task.CompletedTask;
        }
    }
}
