using System.ComponentModel.DataAnnotations;

namespace Brightloop.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string Role { get; set; } = "Customer"; // "Admin", "Staff", "Customer"

        public string? CompanyName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }

    public class Product
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string ProductType { get; set; } = "Software"; // "Software", "Service", "AddOn"

        public decimal SalesPrice { get; set; }
        public decimal CostPrice { get; set; }

        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    }

    public class ProductVariant
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Required, MaxLength(100)]
        public string VariantName { get; set; } = string.Empty; // e.g. "Brand: Odoo", "Tier: Enterprise"

        public decimal PriceExtra { get; set; } = 0; // Choosing Odoo brand adds 560
        public string? Sku { get; set; }
    }

    public class Plan
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string BillingFrequency { get; set; } = "Monthly"; // Daily, Weekly, Monthly, Yearly

        public decimal BasePrice { get; set; }
        public int MinQuantity { get; set; } = 1;

        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? EndDate { get; set; }

        // Plan Policy Rules
        public bool AutoCloseOnEnd { get; set; } = false;
        public bool AllowEarlyClose { get; set; } = true;
        public bool AllowPause { get; set; } = true;
        public bool AllowRenew { get; set; } = true;

        public bool IsActive { get; set; } = true;
    }

    public class QuotationTemplate
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public int DefaultPlanId { get; set; }
        public Plan? DefaultPlan { get; set; }

        public int ValidityDays { get; set; } = 30;
        public string? TermsAndConditions { get; set; }

        public ICollection<QuotationTemplateLine> Lines { get; set; } = new List<QuotationTemplateLine>();
    }

    public class QuotationTemplateLine
    {
        public int Id { get; set; }
        public int QuotationTemplateId { get; set; }
        public QuotationTemplate? QuotationTemplate { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int? VariantId { get; set; }
        public ProductVariant? Variant { get; set; }

        public int Quantity { get; set; } = 1;
        public decimal DiscountPercent { get; set; } = 0;
    }

    public class Subscription
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string SubscriptionNumber { get; set; } = string.Empty; // e.g. SUB-2026-0001

        public int CustomerId { get; set; }
        public User? Customer { get; set; }

        public int PlanId { get; set; }
        public Plan? Plan { get; set; }

        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiryDate { get; set; }
        public DateTime NextBillingDate { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string PaymentTerms { get; set; } = "Immediate"; // Immediate, 15 Days, 30 Days

        [Required, MaxLength(30)]
        public string Status { get; set; } = "Draft"; // Draft, Quotation, Confirmed, Active, Paused, Closed

        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public string? AppliedDiscountCode { get; set; }
        public DateTime? LastInvoicedDate { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<SubscriptionLine> Lines { get; set; } = new List<SubscriptionLine>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }

    public class SubscriptionLine
    {
        public int Id { get; set; }
        public int SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int? VariantId { get; set; }
        public ProductVariant? Variant { get; set; }

        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal TaxPercent { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class Invoice
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty; // e.g. INV-2026-0001

        public int SubscriptionId { get; set; }
        public Subscription? Subscription { get; set; }

        public int CustomerId { get; set; }
        public User? Customer { get; set; }

        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(15);

        public DateTime BillingPeriodStart { get; set; }
        public DateTime BillingPeriodEnd { get; set; }

        public decimal SubTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; } = 0;

        [Required, MaxLength(30)]
        public string Status { get; set; } = "Draft"; // Draft, Confirmed, Paid, Cancelled

        public string? Notes { get; set; }
        public bool SentViaEmail { get; set; } = false;
        public DateTime? SentAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class InvoiceLine
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class Payment
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string PaymentNumber { get; set; } = string.Empty; // PAY-2026-0001

        public int InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        public int CustomerId { get; set; }
        public User? Customer { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public decimal Amount { get; set; }

        [Required, MaxLength(50)]
        public string PaymentMethod { get; set; } = "Bank Transfer"; // Bank Transfer, Credit Card, Cash, UPI

        public string? ReferenceNote { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TaxRule
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty; // Standard GST 18%, VAT 10%

        public decimal TaxPercent { get; set; } = 18;
        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
    }

    public class DiscountRule
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty; // e.g. "ODOPROMO10", "VOLUME15"

        [Required, MaxLength(20)]
        public string DiscountType { get; set; } = "Percentage"; // "Percentage", "Fixed"

        public decimal Value { get; set; } = 10; // 10% or $10

        [Required, MaxLength(20)]
        public string AppliesTo { get; set; } = "Subscription"; // "Subscription", "Product"

        public int? SpecificProductId { get; set; }
        public Product? SpecificProduct { get; set; }

        public int? MinQuantity { get; set; }
        public decimal? MinAmount { get; set; }

        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime EndDate { get; set; } = DateTime.UtcNow.AddYears(1);

        public int MaxUsageLimit { get; set; } = 100;
        public int TimesUsed { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }

    public class AiLog
    {
        public int Id { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public string? Category { get; set; } // "ChurnPrediction", "Query", "Upsell"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
