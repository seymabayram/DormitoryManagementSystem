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
        public async Task<IActionResult> ExportExcel()
        {
            var allDues = await _context.DuesAndPenalties!
                .Include(d => d.Student)
                    .ThenInclude(s => s!.Room)
                .ToListAsync();

            using var workbook = new ClosedXML.Excel.XLWorkbook();

            // Sheet 1: Financial Summary
            var summarySheet = workbook.Worksheets.Add("Financial Summary");
            summarySheet.Cell(1, 1).Value = "Dormitory Management System - Financial Report";
            summarySheet.Cell(1, 1).Style.Font.Bold = true;
            summarySheet.Cell(1, 1).Style.Font.FontSize = 14;
            summarySheet.Cell(2, 1).Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";
            summarySheet.Cell(2, 1).Style.Font.Italic = true;

            summarySheet.Cell(4, 1).Value = "Metric";
            summarySheet.Cell(4, 2).Value = "Value (₺)";
            summarySheet.Range("A4:B4").Style.Font.Bold = true;
            summarySheet.Range("A4:B4").Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;

            var collected = allDues.Where(d => d.IsPaid).Sum(d => d.Amount);
            var overdue   = allDues.Where(d => !d.IsPaid).Sum(d => d.Amount);
            var total     = collected + overdue;
            var rate      = total > 0 ? Math.Round((double)(collected / total) * 100, 2) : 0;

            summarySheet.Cell(5, 1).Value = "Collected Revenue";   summarySheet.Cell(5, 2).Value = (double)collected;
            summarySheet.Cell(6, 1).Value = "Total Overdue";       summarySheet.Cell(6, 2).Value = (double)overdue;
            summarySheet.Cell(7, 1).Value = "Total Target";        summarySheet.Cell(7, 2).Value = (double)total;
            summarySheet.Cell(8, 1).Value = "Collection Rate (%)"; summarySheet.Cell(8, 2).Value = rate;
            summarySheet.Columns().AdjustToContents();

            // Sheet 2: Overdue Payments
            var overdueSheet = workbook.Worksheets.Add("Overdue Payments");
            overdueSheet.Cell(1, 1).Value = "Student Name";
            overdueSheet.Cell(1, 2).Value = "Room";
            overdueSheet.Cell(1, 3).Value = "Description";
            overdueSheet.Cell(1, 4).Value = "Amount (₺)";
            overdueSheet.Cell(1, 5).Value = "Due Date";
            overdueSheet.Cell(1, 6).Value = "Days Overdue";
            overdueSheet.Row(1).Style.Font.Bold = true;
            overdueSheet.Row(1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#f8d7da");

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

            // Sheet 3: All Payments
            var allSheet = workbook.Worksheets.Add("All Payments");
            allSheet.Cell(1, 1).Value = "Student Name";
            allSheet.Cell(1, 2).Value = "Room";
            allSheet.Cell(1, 3).Value = "Description";
            allSheet.Cell(1, 4).Value = "Amount (₺)";
            allSheet.Cell(1, 5).Value = "Due Date";
            allSheet.Cell(1, 6).Value = "Status";
            allSheet.Row(1).Style.Font.Bold = true;
            allSheet.Row(1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;

            int allRow = 2;
            foreach (var d in allDues.OrderBy(x => x.DueDate))
            {
                allSheet.Cell(allRow, 1).Value = d.Student?.FullName ?? "Unknown";
                allSheet.Cell(allRow, 2).Value = d.Student?.Room?.RoomNumber ?? "N/A";
                allSheet.Cell(allRow, 3).Value = d.Description;
                allSheet.Cell(allRow, 4).Value = (double)d.Amount;
                allSheet.Cell(allRow, 5).Value = d.DueDate.ToString("dd/MM/yyyy");
                allSheet.Cell(allRow, 6).Value = d.IsPaid ? "Paid" : "Unpaid";
                allSheet.Cell(allRow, 6).Style.Font.FontColor = d.IsPaid
                    ? ClosedXML.Excel.XLColor.Green
                    : ClosedXML.Excel.XLColor.Red;
                allRow++;
            }
            allSheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"DormitoryReport_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ── PDF PRINT PAGE ──
        public async Task<IActionResult> PrintReport()
        {
            var allDues = await _context.DuesAndPenalties!
                .Include(d => d.Student)
                    .ThenInclude(s => s!.Room)
                .ToListAsync();

            var collected = allDues.Where(d => d.IsPaid).Sum(d => d.Amount);
            var overdue   = allDues.Where(d => !d.IsPaid).Sum(d => d.Amount);
            var total     = collected + overdue;
            var rate      = total > 0 ? Math.Round((double)(collected / total) * 100, 2) : 0;

            ViewBag.CollectedRevenue = collected;
            ViewBag.TotalOverdue     = overdue;
            ViewBag.TotalTarget      = total;
            ViewBag.CollectionRate   = rate;
            ViewBag.DataAsOf         = DateTime.Now.ToString("MMMM d, yyyy HH:mm");
            ViewBag.AllDues          = allDues.OrderBy(d => d.DueDate).ToList();

            return View();
        }
    }
}
