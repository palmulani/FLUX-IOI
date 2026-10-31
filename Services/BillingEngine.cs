using Brightloop.Data;
using Brightloop.Models;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Services
{
    public class BillingEngine : IBillingEngine
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<BillingEngine> _logger;

        public BillingEngine(AppDbContext context, IEmailService emailService, ILogger<BillingEngine> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<BillingRunResult> RunBillingDayAsync(bool autoConfirm = true, bool sendEmails = true)
        {
            var result = new BillingRunResult();
            var now = DateTime.UtcNow;

            // Find all active subscriptions where NextBillingDate is due (or today/past)
            var dueSubscriptions = await _context.Subscriptions
                .Include(s => s.Customer)
                .Include(s => s.Plan)
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Product)
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Variant)
                .Where(s => s.Status == "Active" && s.NextBillingDate <= now.AddHours(2))
                .ToListAsync();

            result.TotalSubscriptionsChecked = dueSubscriptions.Count;

            // Fetch default tax rule
            var defaultTax = await _context.TaxRules.FirstOrDefaultAsync(t => t.IsDefault && t.IsActive)
                             ?? new TaxRule { Name = "Standard GST", TaxPercent = 18m };

            foreach (var sub in dueSubscriptions)
            {
                // Anti-duplicate protection: Check if an invoice was already generated for this billing period
                var periodStart = sub.LastInvoicedDate ?? sub.StartDate;
                var periodEnd = sub.NextBillingDate;

                var existingInvoice = await _context.Invoices
                    .AnyAsync(i => i.SubscriptionId == sub.Id &&
                                   i.BillingPeriodStart.Date == periodStart.Date &&
                                   i.Status != "Cancelled");

                if (existingInvoice)
                {
                    result.SkippedAlreadyBilledCount++;
                    result.LogMessages.Add($"Subscription {sub.SubscriptionNumber}: Already billed for period {periodStart:yyyy-MM-dd}. Skipped to prevent duplicate billing.");
                    continue;
                }

                // Generate new Invoice
                var invoiceCount = await _context.Invoices.CountAsync() + 1;
                var invNumber = $"INV-{now.Year}-{invoiceCount:D4}";

                var invoice = new Invoice
                {
                    InvoiceNumber = invNumber,
                    SubscriptionId = sub.Id,
                    CustomerId = sub.CustomerId,
                    InvoiceDate = now,
                    DueDate = now.AddDays(sub.PaymentTerms == "Immediate" ? 3 : (sub.PaymentTerms == "15 Days" ? 15 : 30)),
                    BillingPeriodStart = periodStart,
                    BillingPeriodEnd = periodEnd,
                    Status = autoConfirm ? "Confirmed" : "Draft",
                    CreatedAt = now
                };

                decimal subTotal = 0;
                decimal taxTotal = 0;
                decimal discountTotal = 0;

                // Create invoice lines from subscription lines
                foreach (var line in sub.Lines)
                {
                    var unitPrice = line.UnitPrice;
                    var qty = line.Quantity;
                    var lineAmount = unitPrice * qty;

                    var taxRate = line.TaxPercent > 0 ? line.TaxPercent : defaultTax.TaxPercent;
                    var lineTax = Math.Round(lineAmount * (taxRate / 100m), 2);
                    var lineDiscount = line.DiscountAmount;

                    subTotal += lineAmount;
                    taxTotal += lineTax;
                    discountTotal += lineDiscount;

                    var desc = line.Product?.Name ?? "Subscription Item";
                    if (line.Variant != null)
                    {
                        desc += $" ({line.Variant.VariantName})";
                    }

                    invoice.Lines.Add(new InvoiceLine
                    {
                        ProductId = line.ProductId,
                        Description = desc,
                        Quantity = qty,
                        UnitPrice = unitPrice,
                        TaxAmount = lineTax,
                        DiscountAmount = lineDiscount,
                        LineTotal = lineAmount + lineTax - lineDiscount
                    });
                }

                // Apply Subscription-Level Discount if any
                if (!string.IsNullOrEmpty(sub.AppliedDiscountCode))
                {
                    var discRule = await _context.DiscountRules
                        .FirstOrDefaultAsync(d => d.Code == sub.AppliedDiscountCode && d.IsActive && d.EndDate >= now);

                    if (discRule != null && discRule.TimesUsed < discRule.MaxUsageLimit)
                    {
                        decimal addlDiscount = 0;
                        if (discRule.DiscountType == "Percentage")
                        {
                            addlDiscount = Math.Round(subTotal * (discRule.Value / 100m), 2);
                        }
                        else
                        {
                            addlDiscount = discRule.Value;
                        }

                        discountTotal += addlDiscount;
                        discRule.TimesUsed++;
                    }
                }

                invoice.SubTotal = subTotal;
                invoice.TaxTotal = taxTotal;
                invoice.DiscountTotal = discountTotal;
                invoice.TotalAmount = Math.Max(0, subTotal + taxTotal - discountTotal);

                _context.Invoices.Add(invoice);

                // Advance Next Billing Date
                sub.LastInvoicedDate = now;
                var freq = sub.Plan?.BillingFrequency ?? "Monthly";
                sub.NextBillingDate = freq switch
                {
                    "Daily" => sub.NextBillingDate.AddDays(1),
                    "Weekly" => sub.NextBillingDate.AddDays(7),
                    "Monthly" => sub.NextBillingDate.AddMonths(1),
                    "Yearly" => sub.NextBillingDate.AddYears(1),
                    _ => sub.NextBillingDate.AddMonths(1)
                };

                // Check Plan Policy: AutoCloseOnEnd
                if (sub.Plan != null && sub.Plan.AutoCloseOnEnd && sub.Plan.EndDate.HasValue && sub.Plan.EndDate.Value <= now)
                {
                    sub.Status = "Closed";
                    result.LogMessages.Add($"Subscription {sub.SubscriptionNumber} reached plan end date and was closed per plan policy.");
                }

                result.InvoicesCreatedCount++;
                result.TotalAmountBilled += invoice.TotalAmount;
                result.GeneratedInvoiceNumbers.Add(invNumber);
                result.LogMessages.Add($"Generated {invNumber} for {sub.Customer?.FullName ?? "Customer"} - ${invoice.TotalAmount:F2}");

                // Email notification
                if (sendEmails && sub.Customer != null && !string.IsNullOrEmpty(sub.Customer.Email))
                {
                    _ = _emailService.SendInvoiceEmailAsync(sub.Customer.Email, sub.Customer.FullName, invNumber, invoice.TotalAmount, invoice.DueDate);
                    invoice.SentViaEmail = true;
                    invoice.SentAt = now;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        public async Task<Invoice> GenerateInvoiceForSubscriptionAsync(int subscriptionId, bool sendEmail = true)
        {
            var sub = await _context.Subscriptions
                .Include(s => s.Customer)
                .Include(s => s.Plan)
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Product)
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Variant)
                .FirstOrDefaultAsync(s => s.Id == subscriptionId);

            if (sub == null) throw new ArgumentException("Subscription not found");

            var now = DateTime.UtcNow;
            var invoiceCount = await _context.Invoices.CountAsync() + 1;
            var invNumber = $"INV-{now.Year}-{invoiceCount:D4}";

            var invoice = new Invoice
            {
                InvoiceNumber = invNumber,
                SubscriptionId = sub.Id,
                CustomerId = sub.CustomerId,
                InvoiceDate = now,
                DueDate = now.AddDays(15),
                BillingPeriodStart = sub.LastInvoicedDate ?? sub.StartDate,
                BillingPeriodEnd = sub.NextBillingDate,
                Status = "Confirmed",
                CreatedAt = now
            };

            decimal subTotal = 0;
            decimal taxTotal = 0;
            decimal discountTotal = 0;

            foreach (var line in sub.Lines)
            {
                var unitPrice = line.UnitPrice;
                var qty = line.Quantity;
                var lineAmount = unitPrice * qty;
                var lineTax = line.TaxAmount;
                var lineDiscount = line.DiscountAmount;

                subTotal += lineAmount;
                taxTotal += lineTax;
                discountTotal += lineDiscount;

                var desc = line.Product?.Name ?? "Item";
                if (line.Variant != null) desc += $" ({line.Variant.VariantName})";

                invoice.Lines.Add(new InvoiceLine
                {
                    ProductId = line.ProductId,
                    Description = desc,
                    Quantity = qty,
                    UnitPrice = unitPrice,
                    TaxAmount = lineTax,
                    DiscountAmount = lineDiscount,
                    LineTotal = lineAmount + lineTax - lineDiscount
                });
            }

            invoice.SubTotal = subTotal;
            invoice.TaxTotal = taxTotal;
            invoice.DiscountTotal = discountTotal;
            invoice.TotalAmount = Math.Max(0, subTotal + taxTotal - discountTotal);

            _context.Invoices.Add(invoice);
            sub.LastInvoicedDate = now;

            if (sendEmail && sub.Customer != null && !string.IsNullOrEmpty(sub.Customer.Email))
            {
                _ = _emailService.SendInvoiceEmailAsync(sub.Customer.Email, sub.Customer.FullName, invNumber, invoice.TotalAmount, invoice.DueDate);
                invoice.SentViaEmail = true;
                invoice.SentAt = now;
            }

            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task<decimal> CalculateTaxesAsync(decimal taxableAmount, int? taxRuleId = null)
        {
            var rule = taxRuleId.HasValue
                ? await _context.TaxRules.FindAsync(taxRuleId.Value)
                : await _context.TaxRules.FirstOrDefaultAsync(t => t.IsDefault && t.IsActive);

            var rate = rule?.TaxPercent ?? 18m;
            return Math.Round(taxableAmount * (rate / 100m), 2);
        }

        public async Task<(decimal discountAmount, string? appliedCode)> CalculateDiscountAsync(decimal amount, int quantity, string? requestedCode, int? productId = null)
        {
            if (string.IsNullOrWhiteSpace(requestedCode)) return (0m, null);

            var now = DateTime.UtcNow;
            var rule = await _context.DiscountRules
                .FirstOrDefaultAsync(d => d.Code.ToUpper() == requestedCode.Trim().ToUpper() &&
                                          d.IsActive &&
                                          d.StartDate <= now &&
                                          d.EndDate >= now);

            if (rule == null) return (0m, null);
            if (rule.TimesUsed >= rule.MaxUsageLimit) return (0m, null);
            if (rule.MinQuantity.HasValue && quantity < rule.MinQuantity.Value) return (0m, null);
            if (rule.MinAmount.HasValue && amount < rule.MinAmount.Value) return (0m, null);
            if (rule.AppliesTo == "Product" && rule.SpecificProductId.HasValue && productId.HasValue && rule.SpecificProductId.Value != productId.Value)
            {
                return (0m, null);
            }

            decimal disc = 0;
            if (rule.DiscountType == "Percentage")
            {
                disc = Math.Round(amount * (rule.Value / 100m), 2);
            }
            else
            {
                disc = Math.Min(amount, rule.Value);
            }

            return (disc, rule.Code);
        }
    }
}
