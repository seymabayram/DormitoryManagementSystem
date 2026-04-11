using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Controllers
{
    [Authorize]
    public class DuesController : Controller
    {
        private readonly AppDbContext _context;

        public DuesController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var query = _context.DuesAndPenalties.AsNoTracking().Include(d => d.Student!).ThenInclude(s => s!.Room).AsQueryable();
            if (User.IsInRole("Student"))
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
                    if (student != null)
                        query = query.Where(d => d.StudentId == student.Id);
                    else
                        query = query.Where(d => false); // hide all if no profile
                }
            }
            return View(await query.ToListAsync());
        }

        // GET: Dues/Create
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> Create()
        {
            var students = await _context.Students.AsNoTracking().ToListAsync();
            ViewData["Students"] = students.Select(s => new SelectListItem 
            { 
                Value = s.Id.ToString(), 
                Text = s.FullNameWithRegNo 
            }).ToList();
            
            return View(new DuesAndPenalty());
        }

        // POST: Dues/Create
        [HttpPost]
        [Authorize(Roles = "Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StudentId,Amount,DueDate,Description")] DuesAndPenalty due)
        {
            if (ModelState.IsValid)
            {
                _context.Add(due);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Due/Penalty record created successfully.";
                return RedirectToAction(nameof(Index));
            }
            var students = await _context.Students.AsNoTracking().ToListAsync();
            ViewData["Students"] = students.Select(s => new SelectListItem 
            { 
                Value = s.Id.ToString(), 
                Text = s.FullNameWithRegNo 
            }).ToList();

            return View(due);
        }

        // POST: Dues/MarkAsPaid/5
        [HttpPost]
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var due = await _context.DuesAndPenalties.FindAsync(id);
            if (due != null)
            {
                due.IsPaid = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Record marked as paid.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
