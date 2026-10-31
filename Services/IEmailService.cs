namespace Brightloop.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink);
        Task<bool> SendInvoiceEmailAsync(string toEmail, string customerName, string invoiceNumber, decimal amount, DateTime dueDate);
        Task<bool> SendPaymentReceiptEmailAsync(string toEmail, string customerName, string paymentNumber, decimal amount, string invoiceNumber);
    }
}
