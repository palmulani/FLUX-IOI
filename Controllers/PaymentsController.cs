using Brightloop.Data;
using Brightloop.Models;
using Brightloop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class PaymentsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public PaymentsController(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index()
        {
            var payments = await _context.Payments
                .Include(p => p.Customer)
                .Include(p => p.Invoice)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            return View(payments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPayment(int invoiceId, decimal amount, string paymentMethod, DateTime? paymentDate, string? referenceNote)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
            {
                TempData["Error"] = "Invoice not found.";
                return RedirectToAction("Index", "Invoices");
            }

            var balanceDue = invoice.TotalAmount - invoice.PaidAmount;
            if (amount <= 0 || amount > balanceDue)
            {
                TempData["Error"] = $"Invalid payment amount. Balance due is ${balanceDue:F2}.";
                return RedirectToAction("Details", "Invoices", new { id = invoiceId });
            }

            var count = await _context.Payments.CountAsync() + 1;
            var payNumber = $"PAY-{DateTime.UtcNow.Year}-{count:D4}";

            var payment = new Payment
            {
                PaymentNumber = payNumber,
                InvoiceId = invoice.Id,
                CustomerId = invoice.CustomerId,
                Amount = amount,
                PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Bank Transfer" : paymentMethod,
                PaymentDate = paymentDate ?? DateTime.UtcNow,
                ReferenceNote = referenceNote ?? "Payment received",
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            invoice.PaidAmount += amount;

            if (invoice.PaidAmount >= invoice.TotalAmount)
            {
                invoice.Status = "Paid";
            }

            await _context.SaveChangesAsync();

            // Send receipt email
            if (invoice.Customer != null && !string.IsNullOrEmpty(invoice.Customer.Email))
            {
                _ = _emailService.SendPaymentReceiptEmailAsync(invoice.Customer.Email, invoice.Customer.FullName, payNumber, amount, invoice.InvoiceNumber);
            }

            TempData["Success"] = $"Payment {payNumber} of ${amount:F2} recorded successfully via {payment.PaymentMethod}. Invoice balance updated.";
            return RedirectToAction("Details", "Invoices", new { id = invoiceId });
        }
    }
}
