using Brightloop.Data;
using Brightloop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class PlansController : Controller
    {
        private readonly AppDbContext _context;

        public PlansController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var plans = await _context.Plans.OrderBy(p => p.Name).ToListAsync();
            return View(plans);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new Plan
            {
                StartDate = DateTime.UtcNow,
                BillingFrequency = "Monthly",
                AllowEarlyClose = true,
                AllowPause = true,
                AllowRenew = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Plan plan)
        {
            if (string.IsNullOrWhiteSpace(plan.Name) || string.IsNullOrWhiteSpace(plan.Code))
            {
                ModelState.AddModelError("", "Plan Name and Code are required.");
                return View(plan);
            }

            _context.Plans.Add(plan);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Subscription Plan '{plan.Name}' created successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var plan = await _context.Plans.FindAsync(id);
            if (plan != null)
            {
                plan.IsActive = !plan.IsActive;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Plan '{plan.Name}' is now {(plan.IsActive ? "Active" : "Inactive")}.";
            }
            return RedirectToAction("Index");
        }
    }
}
