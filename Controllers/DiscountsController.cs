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
    }
}
