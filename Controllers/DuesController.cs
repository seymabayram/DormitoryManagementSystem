using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
            var query = _context.DuesAndPenalties.Include(d => d.Student).AsQueryable();
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
        [Authorize(Roles = "Admin,Staff")]
        public IActionResult Create()
        {
            ViewBag.Students = _context.Students.ToList();
            return View();
        }

        // POST: Dues/Create
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StudentId,Amount,DueDate,Description")] DuesAndPenalty due)
        {
            if (ModelState.IsValid)
            {
                _context.Add(due);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Students = _context.Students.ToList();
            return View(due);
        }

        // POST: Dues/MarkAsPaid/5
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var due = await _context.DuesAndPenalties.FindAsync(id);
            if (due != null)
            {
                due.IsPaid = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
