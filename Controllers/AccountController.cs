using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // ─────────────── LOGIN ───────────────
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            return View(new LoginViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Skip server-side Required on Username — it depends on role
            ModelState.Remove("Username");
            ModelState.Remove("NationalId");

            if (!ModelState.IsValid)
                return View(model);

            User? user = null;

            if (model.SelectedRole == "Student")
            {
                // Students log in with TC identity number + password
                if (string.IsNullOrWhiteSpace(model.NationalId))
                {
                    ModelState.AddModelError("NationalId", "TC Identity Number is required.");
                    return View(model);
                }

                var student = await _context.Students
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.NationalId == model.NationalId);

                if (student?.User != null && student.User.PasswordHash == model.Password && student.User.IsActive)
                    user = student.User;
            }
            else
            {
                // Admin / Staff login with username + password
                if (string.IsNullOrWhiteSpace(model.Username))
                {
                    ModelState.AddModelError("Username", "Username is required.");
                    return View(model);
                }

                var lowerUsername = model.Username.ToLower();
                user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == lowerUsername
                                           && u.PasswordHash == model.Password
                                           && u.IsActive
                                           && u.Role != null && u.Role.RoleName == model.SelectedRole);
            }

            if (user != null)
            {
                // Load role if not already loaded
                if (user.Role == null)
                    await _context.Entry(user).Reference(u => u.Role).LoadAsync();

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "Student")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = model.RememberMe });

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid credentials. Please check your information and try again.");
            return View(model);
        }

        // ─────────────── REGISTER ───────────────
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Check username uniqueness
            var existingUser = await _context.Users.AnyAsync(u => u.Username.ToLower() == model.Username.ToLower());
            if (existingUser)
            {
                ModelState.AddModelError("Username", "This username is already taken.");
                return View(model);
            }

            // Find or create role
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == model.SelectedRole);
            if (role == null)
            {
                ModelState.AddModelError("SelectedRole", "Selected role is invalid.");
                return View(model);
            }

            var newUser = new User
            {
                Username = model.Username,
                PasswordHash = model.Password,
                RoleId = role.Id,
                IsActive = true
            };
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Account created successfully! You can now log in as {model.SelectedRole}.";
            return RedirectToAction("Login");
        }

        // ─────────────── LOGOUT ───────────────
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
