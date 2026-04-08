using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoomsController : Controller
    {
        // Our worker's toolbox (Database connection)
        private readonly AppDbContext _context;

        // Our worker receives the database key when starting the job.
        public RoomsController(AppDbContext context)
        {
            _context = context;
        }

        // MISSION 1: Send the list of rooms to the screen (to the View window).
        public IActionResult Index()
        {
            var rooms = _context.Rooms.ToList(); // Go to the database, grab all the rooms, and put them in a list
            return View(rooms); // Send this list to the "View" window where people can see it
        }

        // MISSION 2: Show the form to add a new room
        public IActionResult Create()
        {
            return View();
        }

        // MISSION 3: Save the room that the user submitted via the form
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("RoomNumber,Capacity")] Room room)
        {
            if (ModelState.IsValid)
            {
                _context.Add(room);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // MISSION 4: Show the room to edit
        public IActionResult Edit(int? id)
        {
            if (id == null) return NotFound();

            var room = _context.Rooms.Find(id);
            if (room == null) return NotFound();

            return View(room);
        }

        // MISSION 5: Save changes to the room
        [HttpPost]
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