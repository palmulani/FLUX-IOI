using Brightloop.Data;
using Brightloop.Models;
using Brightloop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class SubscriptionsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IBillingEngine _billingEngine;

        public SubscriptionsController(AppDbContext context, IBillingEngine billingEngine)
        {
            _context = context;
            _billingEngine = billingEngine;
        }

        public async Task<IActionResult> Index(string? status, string? search)
        {
            var query = _context.Subscriptions
                .Include(s => s.Customer)
                .Include(s => s.Plan)
                .Include(s => s.Lines)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(s => s.Status == status);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.SubscriptionNumber.Contains(search) ||
                                         s.Customer!.FullName.Contains(search) ||
                                         s.Customer!.CompanyName!.Contains(search));
            }

            var list = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
            ViewBag.CurrentStatus = status ?? "All";
            ViewBag.Search = search ?? "";

            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var sub = await _context.Subscriptions
                .Include(s => s.Customer)
                .Include(s => s.Plan)
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Product)
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Variant)
                .Include(s => s.Invoices)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sub == null) return NotFound();

            return View(sub);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? fromTemplateId = null)
        {
            ViewBag.Customers = await _context.Users.Where(u => u.Role == "Customer").ToListAsync();
            ViewBag.Plans = await _context.Plans.Where(p => p.IsActive).ToListAsync();
            ViewBag.Products = await _context.Products.Include(p => p.Variants).Where(p => p.IsActive).ToListAsync();
            ViewBag.Templates = await _context.QuotationTemplates.Include(t => t.Lines).ToListAsync();
            ViewBag.DefaultTax = await _context.TaxRules.FirstOrDefaultAsync(t => t.IsDefault) ?? new TaxRule { TaxPercent = 18m };

            if (fromTemplateId.HasValue)
            {
                var tmpl = await _context.QuotationTemplates
                    .Include(t => t.Lines)
                    .FirstOrDefaultAsync(t => t.Id == fromTemplateId.Value);
                ViewBag.SelectedTemplate = tmpl;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int customerId, int planId, string paymentTerms, string? discountCode,
            int[] productIds, int[]? variantIds, int[] quantities, decimal[] unitPrices, string initialStatus = "Draft")
        {
            var plan = await _context.Plans.FindAsync(planId);
            if (plan == null)
            {
                TempData["Error"] = "Selected Plan not found.";
                return RedirectToAction("Create");
            }

            var count = await _context.Subscriptions.CountAsync() + 1;
            var subNumber = $"SUB-{DateTime.UtcNow.Year}-{count:D4}";

            var sub = new Subscription
            {
                SubscriptionNumber = subNumber,
                CustomerId = customerId,
                PlanId = planId,
                StartDate = DateTime.UtcNow,
                ExpiryDate = plan.EndDate ?? DateTime.UtcNow.AddYears(1),
                NextBillingDate = DateTime.UtcNow,
                PaymentTerms = string.IsNullOrWhiteSpace(paymentTerms) ? "Immediate" : paymentTerms,
                Status = string.IsNullOrWhiteSpace(initialStatus) ? "Draft" : initialStatus,
                CreatedAt = DateTime.UtcNow
            };

            decimal subTotal = 0;
            decimal taxTotal = 0;
            var defaultTax = await _context.TaxRules.FirstOrDefaultAsync(t => t.IsDefault) ?? new TaxRule { TaxPercent = 18m };

            for (int i = 0; i < productIds.Length; i++)
            {
                if (productIds[i] <= 0) continue;
                var pid = productIds[i];
                var vid = (variantIds != null && i < variantIds.Length && variantIds[i] > 0) ? variantIds[i] : (int?)null;
                var qty = (quantities != null && i < quantities.Length && quantities[i] > 0) ? quantities[i] : 1;
                var price = (unitPrices != null && i < unitPrices.Length) ? unitPrices[i] : 0m;

                var lineAmount = price * qty;
                var tax = Math.Round(lineAmount * (defaultTax.TaxPercent / 100m), 2);

                subTotal += lineAmount;
                taxTotal += tax;

                sub.Lines.Add(new SubscriptionLine
                {
                    ProductId = pid,
                    VariantId = vid,
                    Quantity = qty,
                    UnitPrice = price,
                    TaxPercent = defaultTax.TaxPercent,
                    TaxAmount = tax,
                    DiscountAmount = 0,
                    LineTotal = lineAmount + tax
                });
            }

            // Calculate Discount if code supplied
            decimal discountAmount = 0;
            string? appliedCode = null;
            if (!string.IsNullOrWhiteSpace(discountCode))
            {
                var totalQty = sub.Lines.Sum(l => l.Quantity);
                var (disc, code) = await _billingEngine.CalculateDiscountAsync(subTotal, totalQty, discountCode);
                discountAmount = disc;
                appliedCode = code;
            }

            sub.SubTotal = subTotal;
            sub.TaxAmount = taxTotal;
            sub.DiscountAmount = discountAmount;
            sub.AppliedDiscountCode = appliedCode;
            sub.TotalAmount = Math.Max(0, subTotal + taxTotal - discountAmount);

            _context.Subscriptions.Add(sub);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Subscription {sub.SubscriptionNumber} created in '{sub.Status}' state.";
            return RedirectToAction("Details", new { id = sub.Id });
        }

        // State Machine Transition Handlers
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var sub = await _context.Subscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sub == null) return NotFound();

            // Validate policy rules
            if (newStatus == "Paused" && sub.Plan != null && !sub.Plan.AllowPause)
            {
                TempData["Error"] = $"Plan '{sub.Plan.Name}' does not permit pausing.";
                return RedirectToAction("Details", new { id });
            }

            if (newStatus == "Closed" && sub.Plan != null && !sub.Plan.AllowEarlyClose && sub.ExpiryDate > DateTime.UtcNow)
            {
                TempData["Error"] = $"Plan '{sub.Plan.Name}' does not allow early closure.";
                return RedirectToAction("Details", new { id });
            }

            var oldStatus = sub.Status;
            sub.Status = newStatus;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Subscription {sub.SubscriptionNumber} status moved from '{oldStatus}' to '{newStatus}'.";
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateInvoiceNow(int id)
        {
            try
            {
                var invoice = await _billingEngine.GenerateInvoiceForSubscriptionAsync(id, sendEmail: true);
                TempData["Success"] = $"Invoice {invoice.InvoiceNumber} generated for ${invoice.TotalAmount:F2} and sent to customer.";
                return RedirectToAction("Details", "Invoices", new { id = invoice.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error generating invoice: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
        }
    }
}
