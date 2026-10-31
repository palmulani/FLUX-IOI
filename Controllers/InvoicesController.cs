using Brightloop.Data;
using Brightloop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class InvoicesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public InvoicesController(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index(string? filter = "all")
        {
            var now = DateTime.UtcNow;
            var query = _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Subscription)
                .AsQueryable();

            if (filter == "draft") query = query.Where(i => i.Status == "Draft");
            else if (filter == "confirmed") query = query.Where(i => i.Status == "Confirmed");
            else if (filter == "paid") query = query.Where(i => i.Status == "Paid");
            else if (filter == "overdue") query = query.Where(i => i.Status != "Paid" && i.Status != "Cancelled" && i.DueDate < now);
            else if (filter == "cancelled") query = query.Where(i => i.Status == "Cancelled");

            var invoices = await query.OrderByDescending(i => i.InvoiceDate).ToListAsync();
            ViewBag.CurrentFilter = filter ?? "all";

            return View(invoices);
        }

        [Authorize(Roles = "Admin,Staff,Customer")]
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Subscription)
                    .ThenInclude(s => s!.Plan)
                .Include(i => i.Lines)
                    .ThenInclude(l => l.Product)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return NotFound();

            if (User.IsInRole("Customer"))
            {
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(idClaim, out var userId) && invoice.CustomerId != userId)
                {
                    return Forbid();
                }
            }

            return View(invoice);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Staff,Customer")]
        public async Task<IActionResult> Print(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.Subscription)
                .Include(i => i.Lines)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return NotFound();

            // Customer tenant isolation security guard
            if (User.IsInRole("Customer"))
            {
                var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(idClaim, out var userId) && invoice.CustomerId != userId)
                {
                    return Forbid();
                }
            }

            return View(invoice);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice != null && invoice.Status == "Draft")
            {
                invoice.Status = "Confirmed";
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Invoice {invoice.InvoiceNumber} confirmed.";
            }
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice != null && invoice.Status != "Paid")
            {
                invoice.Status = "Cancelled";
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Invoice {invoice.InvoiceNumber} cancelled.";
            }
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendEmail(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice != null && invoice.Customer != null && !string.IsNullOrEmpty(invoice.Customer.Email))
            {
                var sent = await _emailService.SendInvoiceEmailAsync(invoice.Customer.Email, invoice.Customer.FullName, invoice.InvoiceNumber, invoice.TotalAmount, invoice.DueDate);
                invoice.SentViaEmail = true;
                invoice.SentAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                if (sent) TempData["Success"] = $"Invoice sent to {invoice.Customer.Email} successfully via SMTP.";
                else TempData["Info"] = $"Invoice dispatch logged (offline simulation).";
            }
            return RedirectToAction("Details", new { id });
        }
    }
}
