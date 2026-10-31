using Brightloop.Data;
using Brightloop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _context;

        public ProductsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Variants)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return View(products);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Product product, string? variantName, decimal? variantPriceExtra, string? variantSku)
        {
            if (string.IsNullOrWhiteSpace(product.Name))
            {
                ModelState.AddModelError("Name", "Product name is required.");
                return View(product);
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Optional initial variant
            if (!string.IsNullOrWhiteSpace(variantName))
            {
                var variant = new ProductVariant
                {
                    ProductId = product.Id,
                    VariantName = variantName.Trim(),
                    PriceExtra = variantPriceExtra ?? 0m,
                    Sku = variantSku?.Trim()
                };
                _context.ProductVariants.Add(variant);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"Product '{product.Name}' created successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddVariant(int productId, string variantName, decimal priceExtra, string? sku)
        {
            if (string.IsNullOrWhiteSpace(variantName))
            {
                TempData["Error"] = "Variant name is required.";
                return RedirectToAction("Index");
            }

            var variant = new ProductVariant
            {
                ProductId = productId,
                VariantName = variantName.Trim(),
                PriceExtra = priceExtra,
                Sku = sku?.Trim()
            };

            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Variant '{variant.VariantName}' (+${priceExtra:F2}) added.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Product deleted.";
            }
            return RedirectToAction("Index");
        }
    }
}
