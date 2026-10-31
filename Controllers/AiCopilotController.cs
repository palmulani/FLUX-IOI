using Brightloop.Data;
using Brightloop.Models;
using Brightloop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Brightloop.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class AiCopilotController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IAiService _aiService;

        public AiCopilotController(AppDbContext context, IAiService aiService)
        {
            _context = context;
            _aiService = aiService;
        }

        public async Task<IActionResult> Index()
        {
            var logs = await _context.AiLogs
                .OrderByDescending(l => l.CreatedAt)
                .Take(10)
                .ToListAsync();

            ViewBag.Customers = await _context.Users.Where(u => u.Role == "Customer").ToListAsync();
            return View(logs);
        }

        [HttpPost]
        public async Task<IActionResult> Query([FromBody] CopilotQueryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return Json(new { success = false, message = "Prompt cannot be empty." });
            }

            // Gather dynamic financial context to feed the AI
            var activeSubsCount = await _context.Subscriptions.CountAsync(s => s.Status == "Active");
            var totalRev = await _context.Payments.SumAsync(p => (decimal?)p.Amount) ?? 0m;
            var overdueInvoices = await _context.Invoices
                .Where(i => i.Status != "Paid" && i.Status != "Cancelled" && i.DueDate < DateTime.UtcNow)
                .Select(i => new { i.InvoiceNumber, Customer = i.Customer!.FullName, Amount = i.TotalAmount - i.PaidAmount, i.DueDate })
                .ToListAsync();

            var contextData = new
            {
                ActiveSubscriptions = activeSubsCount,
                TotalRevenueCollected = totalRev,
                OverdueCount = overdueInvoices.Count,
                OverdueInvoices = overdueInvoices
            };

            var contextJson = JsonSerializer.Serialize(contextData);
            var reply = await _aiService.AskCopilotAsync(request.Prompt, contextJson);

            // Log query
            _context.AiLogs.Add(new AiLog
            {
                Prompt = request.Prompt,
                Response = reply,
                Category = request.Category ?? "General",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return Json(new { success = true, reply });
        }

        [HttpGet]
        public async Task<IActionResult> ChurnAnalysis(int customerId)
        {
            var customer = await _context.Users.FindAsync(customerId);
            if (customer == null) return NotFound();

            var activeSubs = await _context.Subscriptions.CountAsync(s => s.CustomerId == customerId && s.Status == "Active");
            var totalPaid = await _context.Payments.Where(p => p.CustomerId == customerId).SumAsync(p => (decimal?)p.Amount) ?? 0;
            var overdue = await _context.Invoices.CountAsync(i => i.CustomerId == customerId && i.Status != "Paid" && i.DueDate < DateTime.UtcNow);

            var analysis = await _aiService.AnalyzeChurnRiskAsync(customer.FullName, activeSubs, totalPaid, overdue);
            return Json(new { customer = customer.FullName, analysis });
        }
    }

    public class CopilotQueryRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string? Category { get; set; }
    }
}
