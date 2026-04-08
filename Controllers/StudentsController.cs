using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class StudentsController : Controller
    {
        private readonly AppDbContext _context;

        public StudentsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var students = await _context.Students.Include(s => s.Room).ToListAsync();
            return View(students);
        }

        public IActionResult Create()
        {
            ViewBag.Rooms = _context.Rooms.ToList();
            return View();
        }

        [HttpPost]
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
                ModelState.AddModelError("NationalId", "A student with this TC number already exists.");

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

            ViewBag.Rooms = _context.Rooms.ToList();
            return View(student);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();
            ViewBag.Rooms = _context.Rooms.ToList();
            return View(student);
        }

        [HttpPost]
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

            ViewBag.Rooms = _context.Rooms.ToList();
            return View(student);
        }

        [HttpPost, ActionName("Delete")]
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

        private bool StudentExists(int id)
        {
            return _context.Students.Any(e => e.Id == id);
        }
    }
}
