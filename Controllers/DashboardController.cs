using Brightloop.Data;
using Brightloop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Brightloop.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IBillingEngine _billingEngine;
        private readonly IAiService _aiService;

        public DashboardController(AppDbContext context, IBillingEngine billingEngine, IAiService aiService)
        {
            _context = context;
            _billingEngine = billingEngine;
            _aiService = aiService;
        }

        public async Task<IActionResult> Index(string? filter = "all")
        {
            var sw = Stopwatch.StartNew();

            var now = DateTime.UtcNow;

            // Active Subscriptions
            var activeSubsCount = await _context.Subscriptions
                .CountAsync(s => s.Status == "Active");

            var totalSubsCount = await _context.Subscriptions.CountAsync();

            // Total revenue collected (all payments)
            var totalRevenueCollected = await _context.Payments
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            // Invoices query
            var allInvoices = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Subscription)
                .Where(i => i.Status != "Cancelled")
                .ToListAsync();

            var totalOwed = allInvoices
                .Where(i => i.Status != "Paid")
                .Sum(i => i.TotalAmount - i.PaidAmount);

            var overdueInvoices = allInvoices
                .Where(i => i.Status != "Paid" && i.DueDate < now)
                .OrderBy(i => i.DueDate)
                .ToList();

            var overdueCount = overdueInvoices.Count;
            var overdueAmount = overdueInvoices.Sum(i => i.TotalAmount - i.PaidAmount);

            // MRR & ARR Calculation
            var activeSubscriptions = await _context.Subscriptions
                .Include(s => s.Plan)
                .Where(s => s.Status == "Active")
                .ToListAsync();

            decimal calculatedMrr = 0;
            foreach (var sub in activeSubscriptions)
            {
                var freq = sub.Plan?.BillingFrequency ?? "Monthly";
                calculatedMrr += freq switch
                {
                    "Daily" => sub.TotalAmount * 30m,
                    "Weekly" => sub.TotalAmount * 4.33m,
                    "Monthly" => sub.TotalAmount,
                    "Yearly" => sub.TotalAmount / 12m,
                    _ => sub.TotalAmount
                };
            }

            var calculatedArr = calculatedMrr * 12m;

            // Recent Subscriptions
            var recentSubs = await _context.Subscriptions
                .Include(s => s.Customer)
                .Include(s => s.Plan)
                .OrderByDescending(s => s.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Due for Billing count (subscriptions where NextBillingDate <= now)
            var dueForBillingCount = await _context.Subscriptions
                .CountAsync(s => s.Status == "Active" && s.NextBillingDate <= now.AddHours(2));

            sw.Stop();
            ViewBag.ResponseTimeMs = sw.ElapsedMilliseconds;

            ViewBag.ActiveSubsCount = activeSubsCount;
            ViewBag.TotalSubsCount = totalSubsCount;
            ViewBag.TotalRevenueCollected = totalRevenueCollected;
            ViewBag.TotalOwed = totalOwed;
            ViewBag.OverdueCount = overdueCount;
            ViewBag.OverdueAmount = overdueAmount;
            ViewBag.MRR = calculatedMrr;
            ViewBag.ARR = calculatedArr;
            ViewBag.DueForBillingCount = dueForBillingCount;
            ViewBag.RecentSubscriptions = recentSubs;
            ViewBag.OverdueInvoices = overdueInvoices;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunBillingDay()
        {
            var result = await _billingEngine.RunBillingDayAsync(autoConfirm: true, sendEmails: true);

            TempData["BillingResult"] = $"Billing Day Processed! {result.InvoicesCreatedCount} invoices generated (${result.TotalAmountBilled:F2} total). {result.SkippedAlreadyBilledCount} already up-to-date subscriptions skipped.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetAiInsight()
        {
            var activeSubs = await _context.Subscriptions.CountAsync(s => s.Status == "Active");
            var totalRev = await _context.Payments.SumAsync(p => (decimal?)p.Amount) ?? 0;
            var overdue = await _context.Invoices.CountAsync(i => i.Status != "Paid" && i.DueDate < DateTime.UtcNow);

            var contextJson = $"{{\"ActiveSubscriptions\": {activeSubs}, \"TotalRevenueCollected\": {totalRev}, \"OverdueInvoices\": {overdue}}}";
            var response = await _aiService.AskCopilotAsync("Summarize financial health and give 3 key recommendations.", contextJson);

            return Json(new { insight = response });
        }
    }
}
