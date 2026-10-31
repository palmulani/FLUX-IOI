using Brightloop.Data;
using Brightloop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TaxesController : Controller
    {
        private readonly AppDbContext _context;

        public TaxesController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var rules = await _context.TaxRules.OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name).ToListAsync();
            return View(rules);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, decimal taxPercent, bool isDefault)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Tax name is required.";
                return RedirectToAction("Index");
            }

            if (isDefault)
            {
                var existingDefaults = await _context.TaxRules.Where(t => t.IsDefault).ToListAsync();
                foreach (var t in existingDefaults) t.IsDefault = false;
            }

            var rule = new TaxRule
            {
                Name = name.Trim(),
                TaxPercent = taxPercent,
                IsDefault = isDefault,
                IsActive = true
            };

            _context.TaxRules.Add(rule);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Tax Rule '{rule.Name}' ({rule.TaxPercent}%) created.";
            return RedirectToAction("Index");
        }
    }
}
