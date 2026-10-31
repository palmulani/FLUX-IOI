using System.Security.Claims;
using Brightloop.Data;
using Brightloop.Models;
using Brightloop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Controllers
{
    [Authorize(Roles = "Customer")]
    public class PortalController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public PortalController(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        private int GetCurrentUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : 0;
        }

        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var customer = await _context.Users.FindAsync(userId);
            if (customer == null) return RedirectToAction("Login", "Account");

            var subscriptions = await _context.Subscriptions
                .Include(s => s.Plan)
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Product)
                .Where(s => s.CustomerId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var invoices = await _context.Invoices
                .Include(i => i.Subscription)
                .Where(i => i.CustomerId == userId)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            ViewBag.Customer = customer;
            ViewBag.Invoices = invoices;

            return View(subscriptions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PauseSubscription(int id)
        {
            var userId = GetCurrentUserId();
            var sub = await _context.Subscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.Id == id && s.CustomerId == userId);

            if (sub == null) return NotFound();

            if (sub.Plan != null && !sub.Plan.AllowPause)
            {
                TempData["Error"] = "Your current plan does not allow pausing.";
                return RedirectToAction("Index");
            }

            sub.Status = (sub.Status == "Paused") ? "Active" : "Paused";
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Subscription is now {sub.Status}.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenewSubscription(int id)
        {
            var userId = GetCurrentUserId();
            var sub = await _context.Subscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.Id == id && s.CustomerId == userId);

            if (sub == null) return NotFound();

            if (sub.Plan != null && !sub.Plan.AllowRenew)
            {
                TempData["Error"] = "Your current plan does not allow self-renewal.";
                return RedirectToAction("Index");
            }

            sub.ExpiryDate = (sub.ExpiryDate ?? DateTime.UtcNow).AddMonths(1);
            sub.Status = "Active";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Subscription renewed successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayInvoice(int invoiceId, decimal amount, string paymentMethod)
        {
            var userId = GetCurrentUserId();
            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.CustomerId == userId);

            if (invoice == null) return NotFound();

            var balanceDue = invoice.TotalAmount - invoice.PaidAmount;
            if (amount <= 0 || amount > balanceDue)
            {
                TempData["Error"] = $"Invalid payment amount. Balance due is ${balanceDue:F2}.";
                return RedirectToAction("Index");
            }

            var count = await _context.Payments.CountAsync() + 1;
            var payNumber = $"PAY-PORTAL-{DateTime.UtcNow.Year}-{count:D4}";

            var payment = new Payment
            {
                PaymentNumber = payNumber,
                InvoiceId = invoice.Id,
                CustomerId = userId,
                Amount = amount,
                PaymentMethod = paymentMethod ?? "Credit Card Online",
                PaymentDate = DateTime.UtcNow,
                ReferenceNote = "Customer Portal Instant Checkout",
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            invoice.PaidAmount += amount;

            if (invoice.PaidAmount >= invoice.TotalAmount)
            {
                invoice.Status = "Paid";
            }

            await _context.SaveChangesAsync();

            if (invoice.Customer != null && !string.IsNullOrEmpty(invoice.Customer.Email))
            {
                _ = _emailService.SendPaymentReceiptEmailAsync(invoice.Customer.Email, invoice.Customer.FullName, payNumber, amount, invoice.InvoiceNumber);
            }

            TempData["Success"] = $"Payment of ${amount:F2} processed successfully! Receipt: {payNumber}.";
            return RedirectToAction("Index");
        }
    }
}
