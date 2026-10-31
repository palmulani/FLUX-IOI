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

            var customers = await _context.Users.Where(u => u.Role == "Customer").ToListAsync();

            // Build Executive Customer Health & Churn Risk Matrix
            var healthMatrix = new List<CustomerHealthScoreItem>();
            var now = DateTime.UtcNow;

            foreach (var c in customers)
            {
                var activeSubs = await _context.Subscriptions
                    .Include(s => s.Plan)
                    .Where(s => s.CustomerId == c.Id && s.Status == "Active")
                    .ToListAsync();

                var overdueInvoices = await _context.Invoices
                    .Where(i => i.CustomerId == c.Id && i.Status != "Paid" && i.Status != "Cancelled" && i.DueDate < now)
                    .ToListAsync();

                var totalPaid = (await _context.Payments
                    .Where(p => p.CustomerId == c.Id)
                    .Select(p => p.Amount)
                    .ToListAsync()).Sum();

                decimal monthlyCommitment = activeSubs.Sum(s => s.TotalAmount);
                int overdueCount = overdueInvoices.Count;

                // Heuristic Risk Calculation
                int riskScore = 12; // Base healthy score
                string riskCategory = "Low Risk";
                string recommendedAction = "Schedule Quarterly Business Review & Plan Expansion";

                if (overdueCount > 0)
                {
                    riskScore = Math.Min(95, 45 + (overdueCount * 25));
                    riskCategory = riskScore >= 75 ? "Critical / High Risk" : "Elevated Risk";
                    recommendedAction = $"Deploy automated debt chase & offer Net 30 relief";
                }
                else if (activeSubs.Count == 0)
                {
                    riskScore = 80;
                    riskCategory = "At Risk of Abandonment";
                    recommendedAction = "Engage account with 15% win-back incentive";
                }
                else if (monthlyCommitment > 1000)
                {
                    riskScore = 8;
                    riskCategory = "VIP / Enterprise Champion";
                    recommendedAction = "Present annual renewal bundle with dedicated TAM";
                }

                healthMatrix.Add(new CustomerHealthScoreItem
                {
                    CustomerId = c.Id,
                    FullName = c.FullName,
                    Email = c.Email,
                    Company = c.CompanyName ?? "Independent Enterprise",
                    ActiveSubscriptionCount = activeSubs.Count,
                    MonthlyCommitment = monthlyCommitment,
                    TotalLifetimePaid = totalPaid,
                    OverdueInvoiceCount = overdueCount,
                    RiskScore = riskScore,
                    RiskCategory = riskCategory,
                    RecommendedAction = recommendedAction
                });
            }

            ViewBag.Customers = customers;
            ViewBag.HealthMatrix = healthMatrix;
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
            var paymentAmounts = await _context.Payments.Select(p => p.Amount).ToListAsync();
            var totalRev = paymentAmounts.Sum();
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
            var customerPayments = await _context.Payments.Where(p => p.CustomerId == customerId).Select(p => p.Amount).ToListAsync();
            var totalPaid = customerPayments.Sum();
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

    public class CustomerHealthScoreItem
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public int ActiveSubscriptionCount { get; set; }
        public decimal MonthlyCommitment { get; set; }
        public decimal TotalLifetimePaid { get; set; }
        public int OverdueInvoiceCount { get; set; }
        public int RiskScore { get; set; }
        public string RiskCategory { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
    }
}
