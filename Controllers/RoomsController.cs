using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class RoomsController : Controller
    {
        // Our worker's toolbox (Database connection)
        private readonly AppDbContext _context;

        // Our worker receives the database key when starting the job.
        public RoomsController(AppDbContext context)
        {
            _context = context;
        }

        // MISSION 1: Send the list of rooms to the screen with Paging & Search support
        public async Task<IActionResult> Index(int page = 1, string? search = null)
        {
            int pageSize = 10;
            var query = _context.Rooms.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.RoomNumber.Contains(search));
            }

            int totalItems = await query.CountAsync();
            var rooms = await query
                .OrderBy(r => r.RoomNumber.Length)
                .ThenBy(r => r.RoomNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.SearchTerm = search;
            ViewBag.TotalCount = totalItems;

            return View(rooms);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var room = await _context.Rooms
                .Include(r => r.Students)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (room == null) return NotFound();

            return View(room);
        }

        // MISSION 2: Show the form to add a new room
        [Authorize(Roles = "Admin,Staff")]
        public IActionResult Create()
        {
            return View();
        }

        // MISSION 3: Save the room that the user submitted via the form
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("RoomNumber,Capacity")] Room room)
        {
            // Check for existing room number
            if (_context.Rooms.Any(r => r.RoomNumber == room.RoomNumber))
            {
                ModelState.AddModelError("RoomNumber", "This room number is already registered in the system.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(room);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // MISSION 4: Show the room to edit
        [Authorize(Roles = "Admin,Staff")]
        public IActionResult Edit(int? id)
        {
            if (id == null) return NotFound();

            var room = _context.Rooms.Find(id);
            if (room == null) return NotFound();

            return View(room);
        }

        // MISSION 5: Save changes to the room
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, [Bind("Id,RoomNumber,Capacity")] Room room)
        {
            if (id != room.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(room);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // MISSION 6: Delete a room
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            // Check if there are any students currently assigned to this room
            var hasStudents = await _context.Students.AnyAsync(s => s.RoomId == id);
            if (hasStudents)
            {
                TempData["Error"] = "Cannot delete a room that has students assigned to it. Please move or remove the students first.";
                return RedirectToAction(nameof(Index));
            }

            try 
            {
                _context.Rooms.Remove(room);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Room deleted successfully.";
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = "An error occurred while deleting the room: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}