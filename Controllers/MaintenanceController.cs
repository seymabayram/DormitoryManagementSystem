using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Controllers
{
    [Authorize]
    public class MaintenanceController : Controller
    {
        private readonly AppDbContext _context;

        public MaintenanceController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var query = _context.MaintenanceTickets.Include(m => m.Student).ThenInclude(s => s.Room).AsQueryable();
            if (User.IsInRole("Student"))
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
                    if (student != null)
                        query = query.Where(m => m.StudentId == student.Id);
                    else
                        query = query.Where(m => false);
                }
            }
            return View(await query.OrderByDescending(m => m.Id).ToListAsync());
        }

        public IActionResult Create()
        {
            ViewBag.Students = _context.Students.ToList();
            ViewBag.Rooms = _context.Rooms.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StudentId,RoomId,Issue")] MaintenanceTicket ticket)
        {
            if (User.IsInRole("Student"))
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
                    if (student != null)
                    {
                        ticket.StudentId = student.Id;
                        ticket.RoomId = student.RoomId; // Assume they report for their own room by default
                        ModelState.Remove("StudentId");
                        ModelState.Remove("RoomId");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                ticket.IsResolved = false;
                _context.Add(ticket);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Students = _context.Students.ToList();
            ViewBag.Rooms = _context.Rooms.ToList();
            return View(ticket);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Resolve(int id)
        {
            var ticket = await _context.MaintenanceTickets.FindAsync(id);
            if (ticket != null)
            {
                ticket.IsResolved = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
