using Brightloop.Data;
using Brightloop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class QuotationTemplatesController : Controller
    {
        private readonly AppDbContext _context;

        public QuotationTemplatesController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var templates = await _context.QuotationTemplates
                .Include(t => t.DefaultPlan)
                .Include(t => t.Lines)
                    .ThenInclude(l => l.Product)
                .Include(t => t.Lines)
                    .ThenInclude(l => l.Variant)
                .ToListAsync();

            ViewBag.Plans = await _context.Plans.Where(p => p.IsActive).ToListAsync();
            ViewBag.Products = await _context.Products.Include(p => p.Variants).Where(p => p.IsActive).ToListAsync();

            return View(templates);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, int defaultPlanId, int validityDays, string? terms, int[] productIds, int[]? variantIds, int[] quantities, decimal[] discounts)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Template Name is required.";
                return RedirectToAction("Index");
            }

            var template = new QuotationTemplate
            {
                Name = name.Trim(),
                DefaultPlanId = defaultPlanId,
                ValidityDays = validityDays > 0 ? validityDays : 30,
                TermsAndConditions = terms
            };

            _context.QuotationTemplates.Add(template);
            await _context.SaveChangesAsync();

            for (int i = 0; i < productIds.Length; i++)
            {
                if (productIds[i] <= 0) continue;
                var line = new QuotationTemplateLine
                {
                    QuotationTemplateId = template.Id,
                    ProductId = productIds[i],
                    VariantId = (variantIds != null && i < variantIds.Length && variantIds[i] > 0) ? variantIds[i] : null,
                    Quantity = (quantities != null && i < quantities.Length && quantities[i] > 0) ? quantities[i] : 1,
                    DiscountPercent = (discounts != null && i < discounts.Length) ? discounts[i] : 0m
                };
                _context.QuotationTemplateLines.Add(line);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Quotation Template '{template.Name}' created successfully.";
            return RedirectToAction("Index");
        }
    }
}
