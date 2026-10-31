using Microsoft.EntityFrameworkCore;
using Brightloop.Models;

namespace Brightloop.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<QuotationTemplate> QuotationTemplates => Set<QuotationTemplate>();
        public DbSet<QuotationTemplateLine> QuotationTemplateLines => Set<QuotationTemplateLine>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<SubscriptionLine> SubscriptionLines => Set<SubscriptionLine>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<TaxRule> TaxRules => Set<TaxRule>();
        public DbSet<DiscountRule> DiscountRules => Set<DiscountRule>();
        public DbSet<AiLog> AiLogs => Set<AiLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Unique Email for Users
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Product Variant Cascade
            modelBuilder.Entity<ProductVariant>()
                .HasOne(v => v.Product)
                .WithMany(p => p.Variants)
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Subscription Lines Cascade
            modelBuilder.Entity<SubscriptionLine>()
                .HasOne(l => l.Subscription)
                .WithMany(s => s.Lines)
                .HasForeignKey(l => l.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Invoice Lines Cascade
            modelBuilder.Entity<InvoiceLine>()
                .HasOne(l => l.Invoice)
                .WithMany(i => i.Lines)
                .HasForeignKey(l => l.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Payments
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Quotation Template Lines
            modelBuilder.Entity<QuotationTemplateLine>()
                .HasOne(q => q.QuotationTemplate)
                .WithMany(t => t.Lines)
                .HasForeignKey(q => q.QuotationTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ensure Decimal Precision
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }
        }
    }
}
