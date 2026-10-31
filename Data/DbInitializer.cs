using Brightloop.Models;
using Microsoft.EntityFrameworkCore;

namespace Brightloop.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            context.Database.EnsureCreated();

            if (context.Users.Any())
            {
                return; // Database already seeded
            }

            // 1. Seed Users
            var admin = new User
            {
                FullName = "Brightloop System Administrator",
                Email = "admin@brightloop.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
                Role = "Admin",
                CompanyName = "Brightloop Software HQ",
                Phone = "+1-800-BRIGHT",
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            };

            var staff = new User
            {
                FullName = "Sarah Jenkins (Operations)",
                Email = "staff@brightloop.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Staff@123456"),
                Role = "Staff",
                CompanyName = "Brightloop Software",
                Phone = "+1-800-555-0199",
                CreatedAt = DateTime.UtcNow.AddMonths(-4)
            };

            var customer1 = new User
            {
                FullName = "Marcus Vance",
                Email = "acme@shop.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                Role = "Customer",
                CompanyName = "Acme Retail Store",
                Phone = "+1-555-0142",
                Address = "45 Market St, San Francisco, CA",
                CreatedAt = DateTime.UtcNow.AddMonths(-3)
            };

            var customer2 = new User
            {
                FullName = "Elena Rostova",
                Email = "enterprise@corp.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                Role = "Customer",
                CompanyName = "Global Logistics Corp",
                Phone = "+1-555-0188",
                Address = "100 Wall Street, New York, NY",
                CreatedAt = DateTime.UtcNow.AddMonths(-2)
            };

            var customer3 = new User
            {
                FullName = "Pal Mulani",
                Email = "palmulni1@gmail.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Brightloop@2026"),
                Role = "Customer",
                CompanyName = "Mulani Digital Studio",
                Phone = "+91-98765-43210",
                Address = "Sector 14, Tech Park",
                CreatedAt = DateTime.UtcNow.AddMonths(-1)
            };

            context.Users.AddRange(admin, staff, customer1, customer2, customer3);
            context.SaveChanges();

            // 2. Seed Products & Variants
            var p1 = new Product
            {
                Name = "Brightloop ERP Core",
                ProductType = "Software",
                SalesPrice = 1200m,
                CostPrice = 350m,
                Notes = "Flagship cloud ERP system with inventory, accounting and HR modules."
            };

            var p2 = new Product
            {
                Name = "24/7 Dedicated Support SLA",
                ProductType = "Service",
                SalesPrice = 350m,
                CostPrice = 90m,
                Notes = "Guaranteed 15-minute response time and dedicated technical account manager."
            };

            var p3 = new Product
            {
                Name = "Cloud Storage 500GB Add-On",
                ProductType = "AddOn",
                SalesPrice = 150m,
                CostPrice = 30m,
                Notes = "Encrypted off-site automatic backup with high availability."
            };

            var p4 = new Product
            {
                Name = "AI Copilot & Predictive Analytics",
                ProductType = "Software",
                SalesPrice = 450m,
                CostPrice = 80m,
                Notes = "Machine learning churn prediction and natural language business insights."
            };

            context.Products.AddRange(p1, p2, p3, p4);
            context.SaveChanges();

            // Add Variants (specifically brand variant with Odoo +560)
            var v1_standard = new ProductVariant
            {
                ProductId = p1.Id,
                VariantName = "Brand: Standard Cloud",
                PriceExtra = 0m,
                Sku = "ERP-STD"
            };

            var v1_odoo = new ProductVariant
            {
                ProductId = p1.Id,
                VariantName = "Brand: Odoo Enterprise",
                PriceExtra = 560m, // As specified in problem statement: Odoo brand adds 560
                Sku = "ERP-ODOO-560"
            };

            var v1_custom = new ProductVariant
            {
                ProductId = p1.Id,
                VariantName = "Tier: High Performance Multi-Tenant",
                PriceExtra = 300m,
                Sku = "ERP-HP-300"
            };

            context.ProductVariants.AddRange(v1_standard, v1_odoo, v1_custom);
            context.SaveChanges();

            // 3. Seed Plans
            var planDaily = new Plan
            {
                Name = "Micro Testing Plan (Daily)",
                Code = "PLAN-DAILY",
                BillingFrequency = "Daily",
                BasePrice = 65m,
                MinQuantity = 1,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                AutoCloseOnEnd = true, // Closes on its own
                AllowEarlyClose = true,
                AllowPause = false,
                AllowRenew = true
            };

            var planWeekly = new Plan
            {
                Name = "Agile Sprint Plan (Weekly)",
                Code = "PLAN-WEEKLY",
                BillingFrequency = "Weekly",
                BasePrice = 320m,
                MinQuantity = 1,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                AutoCloseOnEnd = false,
                AllowEarlyClose = true,
                AllowPause = true,
                AllowRenew = true
            };

            var planMonthly = new Plan
            {
                Name = "Brightloop Pro (Monthly)",
                Code = "PLAN-MONTHLY",
                BillingFrequency = "Monthly",
                BasePrice = 950m,
                MinQuantity = 1,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                AutoCloseOnEnd = false,
                AllowEarlyClose = true,
                AllowPause = true,
                AllowRenew = true
            };

            var planYearly = new Plan
            {
                Name = "Enterprise Annual Suite (Yearly)",
                Code = "PLAN-YEARLY",
                BillingFrequency = "Yearly",
                BasePrice = 9800m,
                MinQuantity = 3,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                AutoCloseOnEnd = false,
                AllowEarlyClose = false,
                AllowPause = true,
                AllowRenew = true
            };

            context.Plans.AddRange(planDaily, planWeekly, planMonthly, planYearly);
            context.SaveChanges();

            // 4. Seed Tax Rules
            var taxDefault = new TaxRule
            {
                Name = "Standard GST 18%",
                TaxPercent = 18m,
                IsDefault = true,
                IsActive = true
            };

            var taxVat = new TaxRule
            {
                Name = "EU VAT 10%",
                TaxPercent = 10m,
                IsDefault = false,
                IsActive = true
            };

            var taxZero = new TaxRule
            {
                Name = "Export / Software Zero Tax",
                TaxPercent = 0m,
                IsDefault = false,
                IsActive = true
            };

            context.TaxRules.AddRange(taxDefault, taxVat, taxZero);
            context.SaveChanges();

            // 5. Seed Discount Rules
            var discOdoo = new DiscountRule
            {
                Code = "ODOO2026",
                DiscountType = "Percentage",
                Value = 15m,
                AppliesTo = "Subscription",
                MinQuantity = 1,
                MinAmount = 500m,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddMonths(10),
                MaxUsageLimit = 200,
                TimesUsed = 14,
                IsActive = true
            };

            var discFixed = new DiscountRule
            {
                Code = "SAVINGS500",
                DiscountType = "Fixed",
                Value = 500m,
                AppliesTo = "Subscription",
                MinAmount = 2500m,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(6),
                MaxUsageLimit = 50,
                TimesUsed = 8,
                IsActive = true
            };

            var discProduct = new DiscountRule
            {
                Code = "AIPROMO",
                DiscountType = "Percentage",
                Value = 25m,
                AppliesTo = "Product",
                SpecificProductId = p4.Id,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(3),
                MaxUsageLimit = 100,
                TimesUsed = 22,
                IsActive = true
            };

            context.DiscountRules.AddRange(discOdoo, discFixed, discProduct);
            context.SaveChanges();

            // 6. Seed Quotation Templates
            var tmpl1 = new QuotationTemplate
            {
                Name = "Standard SaaS Starter Pack",
                DefaultPlanId = planMonthly.Id,
                ValidityDays = 30,
                TermsAndConditions = "Payment due Net 15 upon invoice generation. Licenses renew automatically."
            };

            var tmpl2 = new QuotationTemplate
            {
                Name = "Enterprise ERP & AI Cloud Package",
                DefaultPlanId = planYearly.Id,
                ValidityDays = 45,
                TermsAndConditions = "Annual contractual commitment with included 24/7 SLA and quarterly reviews."
            };

            context.QuotationTemplates.AddRange(tmpl1, tmpl2);
            context.SaveChanges();

            var tmpl1Line1 = new QuotationTemplateLine
            {
                QuotationTemplateId = tmpl1.Id,
                ProductId = p1.Id,
                VariantId = v1_standard.Id,
                Quantity = 1,
                DiscountPercent = 0
            };

            var tmpl1Line2 = new QuotationTemplateLine
            {
                QuotationTemplateId = tmpl1.Id,
                ProductId = p2.Id,
                Quantity = 1,
                DiscountPercent = 10
            };

            var tmpl2Line1 = new QuotationTemplateLine
            {
                QuotationTemplateId = tmpl2.Id,
                ProductId = p1.Id,
                VariantId = v1_odoo.Id, // Includes Odoo Brand (+560)
                Quantity = 5,
                DiscountPercent = 5
            };

            var tmpl2Line2 = new QuotationTemplateLine
            {
                QuotationTemplateId = tmpl2.Id,
                ProductId = p4.Id,
                Quantity = 2,
                DiscountPercent = 15
            };

            context.QuotationTemplateLines.AddRange(tmpl1Line1, tmpl1Line2, tmpl2Line1, tmpl2Line2);
            context.SaveChanges();

            // 7. Seed Active Subscriptions
            var sub1 = new Subscription
            {
                SubscriptionNumber = "SUB-2026-0001",
                CustomerId = customer1.Id,
                PlanId = planMonthly.Id,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                ExpiryDate = DateTime.UtcNow.AddMonths(10),
                NextBillingDate = DateTime.UtcNow.AddDays(-2), // Due for billing day!
                PaymentTerms = "Immediate",
                Status = "Active",
                SubTotal = 1760m,
                TaxAmount = 316.80m,
                DiscountAmount = 0m,
                TotalAmount = 2076.80m,
                LastInvoicedDate = DateTime.UtcNow.AddMonths(-1),
                CreatedAt = DateTime.UtcNow.AddMonths(-2)
            };

            var sub2 = new Subscription
            {
                SubscriptionNumber = "SUB-2026-0002",
                CustomerId = customer2.Id,
                PlanId = planYearly.Id,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                ExpiryDate = DateTime.UtcNow.AddMonths(11),
                NextBillingDate = DateTime.UtcNow.AddDays(15),
                PaymentTerms = "30 Days",
                Status = "Active",
                SubTotal = 10450m,
                TaxAmount = 1881.00m,
                DiscountAmount = 500m,
                TotalAmount = 11831.00m,
                AppliedDiscountCode = "SAVINGS500",
                LastInvoicedDate = DateTime.UtcNow.AddMonths(-1),
                CreatedAt = DateTime.UtcNow.AddMonths(-1)
            };

            var sub3 = new Subscription
            {
                SubscriptionNumber = "SUB-2026-0003",
                CustomerId = customer3.Id,
                PlanId = planMonthly.Id,
                StartDate = DateTime.UtcNow.AddDays(-10),
                ExpiryDate = DateTime.UtcNow.AddMonths(6),
                NextBillingDate = DateTime.UtcNow.AddDays(-1), // Due for billing!
                PaymentTerms = "Immediate",
                Status = "Active",
                SubTotal = 1200m,
                TaxAmount = 216m,
                DiscountAmount = 120m,
                TotalAmount = 1296m,
                AppliedDiscountCode = "ODOO2026",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            var sub4_quote = new Subscription
            {
                SubscriptionNumber = "SUB-2026-0004",
                CustomerId = customer1.Id,
                PlanId = planWeekly.Id,
                StartDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(30),
                NextBillingDate = DateTime.UtcNow.AddDays(7),
                PaymentTerms = "15 Days",
                Status = "Quotation", // Quotation in review
                SubTotal = 850m,
                TaxAmount = 153m,
                DiscountAmount = 0m,
                TotalAmount = 1003m,
                CreatedAt = DateTime.UtcNow
            };

            context.Subscriptions.AddRange(sub1, sub2, sub3, sub4_quote);
            context.SaveChanges();

            // Lines for Subscriptions
            var sub1Line1 = new SubscriptionLine
            {
                SubscriptionId = sub1.Id,
                ProductId = p1.Id,
                VariantId = v1_odoo.Id, // 1200 + 560 = 1760
                Quantity = 1,
                UnitPrice = 1760m,
                TaxPercent = 18m,
                TaxAmount = 316.80m,
                LineTotal = 2076.80m
            };

            var sub3Line1 = new SubscriptionLine
            {
                SubscriptionId = sub3.Id,
                ProductId = p1.Id,
                VariantId = v1_standard.Id,
                Quantity = 1,
                UnitPrice = 1200m,
                TaxPercent = 18m,
                TaxAmount = 216m,
                DiscountAmount = 120m,
                LineTotal = 1296m
            };

            context.SubscriptionLines.AddRange(sub1Line1, sub3Line1);
            context.SaveChanges();

            // 8. Seed Invoices & Payments
            var inv1 = new Invoice
            {
                InvoiceNumber = "INV-2026-0001",
                SubscriptionId = sub1.Id,
                CustomerId = customer1.Id,
                InvoiceDate = DateTime.UtcNow.AddMonths(-1),
                DueDate = DateTime.UtcNow.AddDays(-15), // OVERDUE!
                BillingPeriodStart = DateTime.UtcNow.AddMonths(-2),
                BillingPeriodEnd = DateTime.UtcNow.AddMonths(-1),
                SubTotal = 1760m,
                TaxTotal = 316.80m,
                DiscountTotal = 0m,
                TotalAmount = 2076.80m,
                PaidAmount = 1000m, // Partial payment
                Status = "Confirmed", // Outstanding / Partially Paid
                SentViaEmail = true,
                SentAt = DateTime.UtcNow.AddMonths(-1),
                CreatedAt = DateTime.UtcNow.AddMonths(-1)
            };

            var inv2 = new Invoice
            {
                InvoiceNumber = "INV-2026-0002",
                SubscriptionId = sub2.Id,
                CustomerId = customer2.Id,
                InvoiceDate = DateTime.UtcNow.AddMonths(-1),
                DueDate = DateTime.UtcNow.AddDays(15),
                BillingPeriodStart = DateTime.UtcNow.AddMonths(-1),
                BillingPeriodEnd = DateTime.UtcNow.AddMonths(11),
                SubTotal = 10450m,
                TaxTotal = 1881.00m,
                DiscountTotal = 500m,
                TotalAmount = 11831.00m,
                PaidAmount = 11831.00m, // Fully paid
                Status = "Paid",
                SentViaEmail = true,
                SentAt = DateTime.UtcNow.AddMonths(-1),
                CreatedAt = DateTime.UtcNow.AddMonths(-1)
            };

            context.Invoices.AddRange(inv1, inv2);
            context.SaveChanges();

            var inv1Line = new InvoiceLine
            {
                InvoiceId = inv1.Id,
                ProductId = p1.Id,
                Description = "Brightloop ERP Core (Brand: Odoo Enterprise)",
                Quantity = 1,
                UnitPrice = 1760m,
                TaxAmount = 316.80m,
                LineTotal = 2076.80m
            };

            var inv2Line = new InvoiceLine
            {
                InvoiceId = inv2.Id,
                ProductId = p1.Id,
                Description = "Enterprise Annual Suite Subscription",
                Quantity = 1,
                UnitPrice = 10450m,
                TaxAmount = 1881.00m,
                DiscountAmount = 500m,
                LineTotal = 11831.00m
            };

            context.InvoiceLines.AddRange(inv1Line, inv2Line);
            context.SaveChanges();

            // Payments
            var pay1 = new Payment
            {
                PaymentNumber = "PAY-2026-0001",
                InvoiceId = inv1.Id,
                CustomerId = customer1.Id,
                PaymentDate = DateTime.UtcNow.AddDays(-10),
                Amount = 1000m,
                PaymentMethod = "Bank Transfer",
                ReferenceNote = "Wire transfer ref #TXN-99882 (Partial payment)",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            var pay2 = new Payment
            {
                PaymentNumber = "PAY-2026-0002",
                InvoiceId = inv2.Id,
                CustomerId = customer2.Id,
                PaymentDate = DateTime.UtcNow.AddDays(-20),
                Amount = 11831.00m,
                PaymentMethod = "Credit Card",
                ReferenceNote = "Stripe Authorization charge_90123812",
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            };

            context.Payments.AddRange(pay1, pay2);
            context.SaveChanges();
        }
    }
}
