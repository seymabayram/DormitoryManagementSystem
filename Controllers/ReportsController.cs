using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using ClosedXML.Excel;

namespace DormitoryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Fetch all dues (including related entities)
            // Note: Include behaves like a LEFT JOIN but can be an INNER JOIN if the model specifies Required.
            // We construct the query flexibly to ensure no records are dropped.
            var allDues = await _context.DuesAndPenalties!
                .Include(d => d.Student)
                    .ThenInclude(s => s!.Room)
                .AsNoTracking() // Use NoTracking for performance and fresh data
                .ToListAsync();

            // ── KEY FINANCIAL METRICS ──
            var collectedRevenue = allDues.Where(d => d.IsPaid).Sum(d => d.Amount);
            var totalOverdue     = allDues.Where(d => !d.IsPaid).Sum(d => d.Amount);
            var totalTarget      = collectedRevenue + totalOverdue;
            var collectionRate   = totalTarget > 0 
                ? Math.Round((double)(collectedRevenue / totalTarget) * 100, 2) 
                : 0;

            // Highest overdue category
            var penaltyOverdue = allDues.Where(d => !d.IsPaid && (d.Description ?? "").ToLower().Contains("penalty")).Sum(d => d.Amount);
            var feeOverdue     = allDues.Where(d => !d.IsPaid && !(d.Description ?? "").ToLower().Contains("penalty")).Sum(d => d.Amount);
            var highestOverdueArea = penaltyOverdue >= feeOverdue ? "Penalty" : "Monthly Fee";

            ViewBag.CollectedRevenue = collectedRevenue;
            ViewBag.TotalOverdue     = totalOverdue;
            ViewBag.TotalTarget      = totalTarget;
            ViewBag.CollectionRate   = collectionRate;
            ViewBag.HighestOverdueArea = highestOverdueArea;

            // ── MONTHLY REVENUE (PAID VS UNPAID) ──
            var monthlyData = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var month = DateTime.Now.AddMonths(-i);
                var monthStart = new DateTime(month.Year, month.Month, 1);
                var monthEnd   = monthStart.AddMonths(1);

                var paidAmount = allDues
                    .Where(d => d.IsPaid && d.DueDate >= monthStart && d.DueDate < monthEnd)
                    .Sum(d => d.Amount);

                var unpaidAmount = allDues
                    .Where(d => !d.IsPaid && d.DueDate >= monthStart && d.DueDate < monthEnd)
                    .Sum(d => d.Amount);

                monthlyData.Add(new
                {
                    Month = month.ToString("MMM"),
                    Paid = paidAmount,
                    Unpaid = unpaidAmount
                });
            }
            ViewBag.MonthlyData = monthlyData;

            // ── OVERDUE PAYMENTS LIST ──
            var overdueList = allDues
                .Where(d => !d.IsPaid)
                .OrderByDescending(d => (DateTime.Now - d.DueDate).TotalDays)
                .Take(15) // Display more records locally in the list
                .Select(d => new
                {
                    StudentName = d.Student?.FullName ?? "Student Not Found",
                    Room        = d.Student?.Room?.RoomNumber ?? "No Room",
                    Type        = d.Description ?? "No Description",
                    Amount      = d.Amount,
                    DaysOverdue = (int)Math.Max(0, (DateTime.Now - d.DueDate).TotalDays),
                    DueDate     = d.DueDate,
                    StudentId   = d.StudentId,
                    DuesId      = d.Id
                })
                .ToList();

            ViewBag.OverdueList  = overdueList;
            ViewBag.OverdueCount = overdueList.Count;

            // ── SUMMARY FOR MANAGEMENT ──
            var avgOverdueDays = overdueList.Any() ? overdueList.Average(o => o.DaysOverdue) : 0;
            ViewBag.AvgOverdueDays = Math.Round(avgOverdueDays, 0);
            ViewBag.TopOverdueDept = highestOverdueArea;
            ViewBag.Q1Target       = totalTarget * 3;

            ViewBag.DataAsOf = DateTime.Now.ToString("MMMM d, yyyy HH:mm:ss");

            return View();
        }

        // ── EXCEL EXPORT ──
        public async Task<IActionResult> ExportExcel(string type = "consolidated")
        {
            var query = _context.DuesAndPenalties!
                .AsNoTracking()
                .Include(d => d.Student)
                    .ThenInclude(s => s!.Room);

            var allDues = await query.ToListAsync();

            using var workbook = new ClosedXML.Excel.XLWorkbook();

            if (type == "overdue")
            {
                // Sheet: Overdue Details
                var overdueSheet = workbook.Worksheets.Add("Overdue Details");
                overdueSheet.Cell(1, 1).Value = "Student Name";
                overdueSheet.Cell(1, 2).Value = "Room";
                overdueSheet.Cell(1, 3).Value = "Description";
                overdueSheet.Cell(1, 4).Value = "Amount (₺)";
                overdueSheet.Cell(1, 5).Value = "Due Date";
                overdueSheet.Cell(1, 6).Value = "Days Overdue";
                overdueSheet.Range("A1:F1").Style.Font.Bold = true;
                overdueSheet.Range("A1:F1").Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#f8d7da");

                int row = 2;
                foreach (var d in allDues.Where(x => !x.IsPaid).OrderByDescending(x => x.DueDate))
                {
                    overdueSheet.Cell(row, 1).Value = d.Student?.FullName ?? "Unknown";
                    overdueSheet.Cell(row, 2).Value = d.Student?.Room?.RoomNumber ?? "N/A";
                    overdueSheet.Cell(row, 3).Value = d.Description;
                    overdueSheet.Cell(row, 4).Value = (double)d.Amount;
                    overdueSheet.Cell(row, 5).Value = d.DueDate.ToString("dd/MM/yyyy");
                    overdueSheet.Cell(row, 6).Value = (int)Math.Max(0, (DateTime.Now - d.DueDate).TotalDays);
                    row++;
                }
                overdueSheet.Columns().AdjustToContents();
            }
            else
            {
                // Default: Consolidated Summary
                var summarySheet = workbook.Worksheets.Add("Consolidated Summary");
                summarySheet.Cell(1, 1).Value = "Dormitory Management System - Consolidated Report";
                summarySheet.Cell(1, 1).Style.Font.Bold = true;
                summarySheet.Cell(1, 1).Style.Font.FontSize = 14;
                summarySheet.Cell(2, 1).Value = $"Report as of: {DateTime.Now:dd/MM/yyyy HH:mm}";
                summarySheet.Cell(2, 1).Style.Font.Italic = true;

                // Finance Section
                summarySheet.Cell(4, 1).Value = "FINANCIAL METRICS";
                summarySheet.Cell(4, 1).Style.Font.Bold = true;
                summarySheet.Range("A4:B4").Merge().Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;

                var collected = allDues.Where(d => d.IsPaid).Sum(d => d.Amount);
                var overdueDues = allDues.Where(d => !d.IsPaid).Sum(d => d.Amount);
                var totalTarget = collected + overdueDues;
                var rate = totalTarget > 0 ? Math.Round((double)(collected / totalTarget) * 100, 2) : 0;

                summarySheet.Cell(5, 1).Value = "Collected Revenue";   summarySheet.Cell(5, 2).Value = (double)collected;
                summarySheet.Cell(6, 1).Value = "Total Overdue";       summarySheet.Cell(6, 2).Value = (double)overdueDues;
                summarySheet.Cell(7, 1).Value = "Collection Rate (%)"; summarySheet.Cell(7, 2).Value = rate;
                summarySheet.Range("B5:B7").Style.NumberFormat.Format = "#,##0.00";

                // Occupancy Section
                summarySheet.Cell(9, 1).Value = "OCCUPANCY METRICS";
                summarySheet.Cell(9, 1).Style.Font.Bold = true;
                summarySheet.Range("A9:B9").Merge().Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;

                var totalCapacity = await _context.Rooms.SumAsync(r => r.Capacity);
                var occupiedBeds  = await _context.Students.CountAsync(s => s.RoomId != null);
                var occupancyRate = totalCapacity > 0 ? Math.Round((double)occupiedBeds / totalCapacity * 100, 1) : 0;

                summarySheet.Cell(10, 1).Value = "Total Capacity";    summarySheet.Cell(10, 2).Value = totalCapacity;
                summarySheet.Cell(11, 1).Value = "Occupied Beds";    summarySheet.Cell(11, 2).Value = occupiedBeds;
                summarySheet.Cell(12, 1).Value = "Occupancy Rate (%)"; summarySheet.Cell(12, 2).Value = occupancyRate;

                summarySheet.Columns().AdjustToContents();
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileSuffix = type == "overdue" ? "OverdueDetails" : "ConsolidatedSummary";
            var fileName = $"DormitoryReport_{fileSuffix}_{DateTime.Now:yyyyMMdd}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ── PDF PRINT PAGE ──
        public async Task<IActionResult> PrintReport(string type = "consolidated")
        {
            var allDues = await _context.DuesAndPenalties!
                .Include(d => d.Student)
                    .ThenInclude(s => s!.Room)
                .AsNoTracking()
                .ToListAsync();

            var collected = allDues.Where(d => d.IsPaid).Sum(d => d.Amount);
            var overdue   = allDues.Where(d => !d.IsPaid).Sum(d => d.Amount);
            var total     = collected + overdue;
            var rate      = total > 0 ? Math.Round((double)(collected / total) * 100, 2) : 0;

            ViewBag.CollectedRevenue = collected;
            ViewBag.TotalOverdue     = overdue;
            ViewBag.TotalTarget      = total;
            ViewBag.CollectionRate   = rate;
            
            // Occupancy
            var totalCapacity = await _context.Rooms.AsNoTracking().SumAsync(r => r.Capacity);
            var occupiedBeds  = await _context.Students.AsNoTracking().CountAsync(s => s.RoomId > 0);
            ViewBag.TotalCapacity = totalCapacity;
            ViewBag.OccupiedBeds  = occupiedBeds;
            ViewBag.OccupancyRate = totalCapacity > 0 ? Math.Round((double)occupiedBeds / totalCapacity * 100, 1) : 0;

            ViewBag.DataAsOf = DateTime.Now.ToString("MMMM d, yyyy HH:mm");
            ViewBag.ReportType = type;

            if (type == "overdue")
            {
                ViewBag.OverdueList = allDues.Where(d => !d.IsPaid)
                    .OrderByDescending(d => (DateTime.Now - d.DueDate).TotalDays)
                    .Select(d => new {
                        StudentName = d.Student?.FullName ?? "Unknown",
                        Room = d.Student?.Room?.RoomNumber ?? "N/A",
                        Type = d.Description ?? "Fee",
                        Amount = d.Amount,
                        DueDate = d.DueDate,
                        DaysOverdue = (int)Math.Max(0, (DateTime.Now - d.DueDate).TotalDays)
                    }).ToList();
            }

            return View();
        }
    }
}
