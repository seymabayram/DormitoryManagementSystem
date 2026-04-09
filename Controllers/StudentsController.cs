using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using System.Security.Claims;

namespace DormitoryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff,Student")]
    public class StudentsController : Controller
    {
        private readonly AppDbContext _context;

        public StudentsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int page = 1, string? search = null)
        {
            int pageSize = 10;
            var query = _context.Students.AsNoTracking().Include(s => s.Room).AsQueryable();
            
            if (User.IsInRole("Student"))
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    query = query.Where(s => s.UserId == userId);
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.NationalId.Contains(search) || s.Name.Contains(search) || s.Surname.Contains(search));
            }

            int totalItems = await query.CountAsync();
            var students = await query
                .OrderBy(s => s.Surname)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.SearchTerm = search;
            ViewBag.TotalCount = totalItems;

            return View(students);
        }

        [Authorize(Roles = "Admin,Staff")]
        public IActionResult Create()
        {
            ViewBag.Rooms = _context.Rooms.Select(r => new {
                Id = r.Id,
                DisplayText = r.RoomNumber + " - " + (r.Capacity - (r.Students != null ? r.Students.Count : 0)) + " available beds"
            }).ToList();
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Surname,NationalId,Email,PhoneNumber,RoomId,MembStartDate,MembEndDate")] Student student)
        {
            if (student.RoomId == 0)
            {
                ModelState.AddModelError("RoomId", "You must assign the student to a specific room.");
            }
            else
            {
                var room = await _context.Rooms.FindAsync(student.RoomId);
                var currentStudentsCount = await _context.Students.CountAsync(s => s.RoomId == student.RoomId);
                if (room != null && currentStudentsCount >= room.Capacity)
                {
                    ModelState.AddModelError("RoomId", "Room is at full capacity!");
                }
            }

            // Check TC uniqueness
            var tcExists = await _context.Students.AnyAsync(s => s.NationalId == student.NationalId);
            if (tcExists)
                ModelState.AddModelError("NationalId", "This student (National ID) is already registered in the system.");

            ModelState.Remove("User");
            ModelState.Remove("Room");

            if (ModelState.IsValid)
            {
                // Ensure Student role exists
                var studentRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Student");
                if (studentRole == null)
                {
                    studentRole = new Role { RoleName = "Student" };
                    _context.Roles.Add(studentRole);
                    await _context.SaveChangesAsync();
                }

                // Auto-generate username from name.surname
                var baseUsername = $"{student.Name.ToLower()}.{student.Surname.ToLower()}";
                // Ensure unique username
                var counter = 1;
                var finalUsername = baseUsername;
                while (await _context.Users.AnyAsync(u => u.Username == finalUsername))
                {
                    finalUsername = $"{baseUsername}{counter}";
                    counter++;
                }

                // Auto-password: last 4 digits of TC + "@Dorm"
                var autoPassword = student.NationalId.Substring(student.NationalId.Length - 4) + "@Dorm";

                var newUser = new User
                {
                    Username = finalUsername,
                    PasswordHash = autoPassword,
                    RoleId = studentRole.Id,
                    IsActive = true
                };
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                student.UserId = newUser.Id;
                _context.Add(student);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Student registered! Username: {finalUsername} | Auto-password: {autoPassword}";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Rooms = _context.Rooms.Select(r => new {
                Id = r.Id,
                DisplayText = r.RoomNumber + " - " + (r.Capacity - (r.Students != null ? r.Students.Count : 0)) + " available beds"
            }).ToList();
            return View(student);
        }

        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();
            ViewBag.Rooms = _context.Rooms.Select(r => new {
                Id = r.Id,
                DisplayText = r.RoomNumber + " - " + (r.Capacity - (r.Students != null ? r.Students.Count : 0)) + " available beds"
            }).ToList();
            return View(student);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Surname,NationalId,Email,PhoneNumber,RoomId,MembStartDate,MembEndDate,UserId")] Student student)
        {
            if (id != student.Id) return NotFound();

            if (student.RoomId == 0)
            {
                ModelState.AddModelError("RoomId", "You must assign the student to a specific room.");
            }
            else
            {
                var room = await _context.Rooms.FindAsync(student.RoomId);
                var currentStudentsCount = await _context.Students.CountAsync(s => s.RoomId == student.RoomId && s.Id != student.Id);
                if (room != null && currentStudentsCount >= room.Capacity)
                    ModelState.AddModelError("RoomId", "Room is at full capacity!");
            }

            // Check TC uniqueness excluding self
            var tcExists = await _context.Students.AnyAsync(s => s.NationalId == student.NationalId && s.Id != student.Id);
            if (tcExists)
                ModelState.AddModelError("NationalId", "Another student with this TC number already exists.");

            ModelState.Remove("User");
            ModelState.Remove("Room");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StudentExists(student.Id)) return NotFound();
                    else throw;
                }
                TempData["Success"] = "Student profile updated.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Rooms = _context.Rooms.Select(r => new {
                Id = r.Id,
                DisplayText = r.RoomNumber + " - " + (r.Capacity - (r.Students != null ? r.Students.Count : 0)) + " available beds"
            }).ToList();
            return View(student);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Staff,Student")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students
                .Include(s => s.Room)
                .Include(s => s.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (student == null) return NotFound();

            if (User.IsInRole("Student"))
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId) && student.UserId != userId)
                {
                    return Forbid();
                }
            }

            return View(student);
        }

        private bool StudentExists(int id)
        {
            return _context.Students.Any(e => e.Id == id);
        }
    }
}
