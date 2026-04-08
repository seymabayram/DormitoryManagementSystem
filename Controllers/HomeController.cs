using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Data;

namespace DormitoryManagementSystem.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _context;

    public HomeController(ILogger<HomeController> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Yalnızca giriş yapılmışsa bu verileri çek
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            if (User.IsInRole("Student"))
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var student = await _context.Students.Include(s => s.Room).FirstOrDefaultAsync(s => s.UserId == userId);
                    if (student != null)
                    {
                        ViewBag.StudentName = student.FullName;
                        ViewBag.RoomNumber = student.Room?.RoomNumber ?? "N/A";
                        
                        var dues = await _context.DuesAndPenalties.Where(d => d.StudentId == student.Id).ToListAsync();
                        ViewBag.TotalPaid = dues.Where(d => d.IsPaid).Sum(d => d.Amount);
                        ViewBag.TotalUnpaid = dues.Where(d => !d.IsPaid).Sum(d => d.Amount);
                    }
                }
            }
            else
            {
                ViewBag.TotalStudents = await _context.Students.CountAsync();
                ViewBag.TotalCapacity = await _context.Rooms.SumAsync(r => r.Capacity);
                
                var allDues = await _context.DuesAndPenalties.ToListAsync();
                ViewBag.TotalPaid = allDues.Where(d => d.IsPaid).Sum(d => d.Amount);
                ViewBag.TotalUnpaid = allDues.Where(d => !d.IsPaid).Sum(d => d.Amount);
                
                ViewBag.PendingTickets = await _context.MaintenanceTickets.CountAsync(m => !m.IsResolved);
                ViewBag.ResolvedTickets = await _context.MaintenanceTickets.CountAsync(m => m.IsResolved);
            }
            return View();
        }
        
        // Return view to render the Welcome card when not authenticated
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
