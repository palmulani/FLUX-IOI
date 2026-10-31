using Brightloop.Models;

namespace Brightloop.Services
{
    public class BillingRunResult
    {
        public int TotalSubscriptionsChecked { get; set; }
        public int InvoicesCreatedCount { get; set; }
        public int SkippedAlreadyBilledCount { get; set; }
        public decimal TotalAmountBilled { get; set; }
        public List<string> GeneratedInvoiceNumbers { get; set; } = new List<string>();
        public List<string> LogMessages { get; set; } = new List<string>();
    }

    public interface IBillingEngine
    {
        Task<BillingRunResult> RunBillingDayAsync(bool autoConfirm = true, bool sendEmails = true);
        Task<Invoice> GenerateInvoiceForSubscriptionAsync(int subscriptionId, bool sendEmail = true);
        Task<decimal> CalculateTaxesAsync(decimal taxableAmount, int? taxRuleId = null);
        Task<(decimal discountAmount, string? appliedCode)> CalculateDiscountAsync(decimal amount, int quantity, string? requestedCode, int? productId = null);
    }
}
