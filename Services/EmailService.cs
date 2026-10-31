using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Brightloop.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var smtpServer = _config["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                var port = int.Parse(_config["EmailSettings:Port"] ?? "587");
                var senderEmail = _config["EmailSettings:SenderEmail"] ?? "palmulani1@gmail.com";
                var senderName = _config["EmailSettings:SenderName"] ?? "Brightloop Software";
                var password = _config["EmailSettings:Password"]?.Replace(" ", "") ?? "";

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(toEmail, toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                // Connect with StartTls
                await client.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(senderEmail, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to {ToEmail} with subject '{Subject}'", toEmail, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to send email to {ToEmail}: {Message}. Running in local simulation mode.", toEmail, ex.Message);
                // Return false but do not crash the app
                return false;
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                    <h2 style='color: #4f46e5; margin-bottom: 16px;'>Brightloop Software</h2>
                    <h3 style='color: #1e293b;'>Password Reset Request</h3>
                    <p style='color: #475569; font-size: 15px;'>We received a request to reset your password for your Brightloop account.</p>
                    <div style='margin: 28px 0;'>
                        <a href='{resetLink}' style='background-color: #4f46e5; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>Reset Your Password</a>
                    </div>
                    <p style='color: #64748b; font-size: 13px;'>If the button does not work, copy and paste this link in your browser:</p>
                    <p style='color: #4f46e5; font-size: 12px; word-break: break-all;'>{resetLink}</p>
                    <p style='color: #94a3b8; font-size: 12px; margin-top: 24px;'>If you did not request this password reset, please ignore this email.</p>
                </div>";

            return await SendEmailAsync(toEmail, "Brightloop - Password Reset Request", html);
        }

        public async Task<bool> SendInvoiceEmailAsync(string toEmail, string customerName, string invoiceNumber, decimal amount, DateTime dueDate)
        {
            var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                    <h2 style='color: #4f46e5;'>Brightloop Software</h2>
                    <h3 style='color: #1e293b;'>New Subscription Invoice Generated</h3>
                    <p>Dear {customerName},</p>
                    <p>Your subscription invoice <strong>{invoiceNumber}</strong> is ready.</p>
                    <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                        <tr style='background: #f8fafc; border-bottom: 1px solid #e2e8f0;'>
                            <td style='padding: 10px;'><strong>Invoice Number:</strong></td>
                            <td style='padding: 10px;'>{invoiceNumber}</td>
                        </tr>
                        <tr style='border-bottom: 1px solid #e2e8f0;'>
                            <td style='padding: 10px;'><strong>Amount Due:</strong></td>
                            <td style='padding: 10px; font-weight: bold; color: #4f46e5;'>${amount:F2}</td>
                        </tr>
                        <tr style='background: #f8fafc;'>
                            <td style='padding: 10px;'><strong>Due Date:</strong></td>
                            <td style='padding: 10px;'>{dueDate:yyyy-MM-dd}</td>
                        </tr>
                    </table>
                    <p>You can review and pay this invoice directly from your Brightloop Customer Portal.</p>
                </div>";

            return await SendEmailAsync(toEmail, $"Brightloop Invoice {invoiceNumber} (${amount:F2})", html);
        }

        public async Task<bool> SendPaymentReceiptEmailAsync(string toEmail, string customerName, string paymentNumber, decimal amount, string invoiceNumber)
        {
            var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                    <h2 style='color: #10b981;'>Payment Received - Brightloop Software</h2>
                    <p>Dear {customerName},</p>
                    <p>Thank you! We have received your payment of <strong>${amount:F2}</strong> for invoice <strong>{invoiceNumber}</strong>.</p>
                    <p>Receipt Reference: <code>{paymentNumber}</code></p>
                    <p>Your subscription remains active and in good standing.</p>
                </div>";

            return await SendEmailAsync(toEmail, $"Brightloop Payment Receipt {paymentNumber}", html);
        }
    }
}
