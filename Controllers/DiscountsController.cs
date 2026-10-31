using Brightloop.Data;
using Brightloop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DiscountsController : Controller
    {
        private readonly AppDbContext _context;

        public DiscountsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var discounts = await _context.DiscountRules
                .Include(d => d.SpecificProduct)
                .OrderByDescending(d => d.Id)
                .ToListAsync();

            ViewBag.Products = await _context.Products.Where(p => p.IsActive).ToListAsync();
            return View(discounts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DiscountRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.Code))
            {
                TempData["Error"] = "Discount Code is required.";
                return RedirectToAction("Index");
            }

            rule.Code = rule.Code.Trim().ToUpper();

            var exists = await _context.DiscountRules.AnyAsync(d => d.Code == rule.Code);
            if (exists)
            {
                TempData["Error"] = $"Discount code '{rule.Code}' already exists.";
                return RedirectToAction("Index");
            }

            _context.DiscountRules.Add(rule);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Discount Rule '{rule.Code}' created successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var rule = await _context.DiscountRules.FindAsync(id);
            if (rule != null)
            {
                rule.IsActive = !rule.IsActive;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Discount rule {rule.Code} is now {(rule.IsActive ? "Active" : "Disabled")}.";
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ValidatePromo(string code, decimal amount = 0, int qty = 1, int? productId = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Json(new { isValid = false, message = "Please enter a promo or coupon code." });
            }

            var cleanCode = code.Trim().ToUpper();
            var now = DateTime.UtcNow;

            var rule = await _context.DiscountRules
                .Include(d => d.SpecificProduct)
                .FirstOrDefaultAsync(d => d.Code == cleanCode);

            if (rule == null)
            {
                return Json(new { isValid = false, message = $"Promo code '{cleanCode}' is not recognized." });
            }

            if (!rule.IsActive)
            {
                return Json(new { isValid = false, message = $"Promo code '{cleanCode}' is currently inactive or suspended." });
            }

            if (rule.StartDate > now)
            {
                return Json(new { isValid = false, message = $"Promo code '{cleanCode}' becomes active on {rule.StartDate:MMM dd, yyyy}." });
            }

            if (rule.EndDate < now)
            {
                return Json(new { isValid = false, message = $"Promo code '{cleanCode}' expired on {rule.EndDate:MMM dd, yyyy}." });
            }

            if (rule.TimesUsed >= rule.MaxUsageLimit)
            {
                return Json(new { isValid = false, message = $"Redemption quota exhausted ({rule.TimesUsed}/{rule.MaxUsageLimit} redemptions used)." });
            }

            if (rule.MinQuantity.HasValue && qty < rule.MinQuantity.Value)
            {
                return Json(new { isValid = false, message = $"Minimum requirement: {rule.MinQuantity.Value} seats/units (Current cart: {qty})." });
            }

            if (rule.MinAmount.HasValue && amount < rule.MinAmount.Value)
            {
                return Json(new { isValid = false, message = $"Minimum spend threshold not met: Requires ${rule.MinAmount.Value:F2} (Current subtotal: ${amount:F2})." });
            }

            if (rule.AppliesTo == "Product" && rule.SpecificProductId.HasValue && productId.HasValue && rule.SpecificProductId.Value != productId.Value)
            {
                return Json(new { isValid = false, message = $"Rule applies exclusively to product '{rule.SpecificProduct?.Name}'." });
            }

            decimal discountCalculated = 0;
            if (rule.DiscountType == "Percentage")
            {
                discountCalculated = Math.Round(amount * (rule.Value / 100m), 2);
            }
            else
            {
                discountCalculated = Math.Min(amount, rule.Value);
            }

            var finalAmount = Math.Max(0, amount - discountCalculated);

            return Json(new
            {
                isValid = true,
                code = rule.Code,
                discountType = rule.DiscountType,
                discountValue = rule.Value,
                discountAmount = discountCalculated,
                finalAmount = finalAmount,
                message = $"Coupon applied successfully! {(rule.DiscountType == "Percentage" ? $"{rule.Value}% discount" : $"${rule.Value:F2} fixed discount")} (-${discountCalculated:F2})"
            });
        }
    }
}
