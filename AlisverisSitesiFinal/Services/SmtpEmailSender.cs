using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace AlisverisSitesiFinal.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var host = _config["Email:Smtp:Host"];
            var port = int.Parse(_config["Email:Smtp:Port"] ?? "587");
            var user = _config["Email:Smtp:User"];
            var pass = _config["Email:Smtp:Password"];
            var from = _config["Email:Smtp:From"] ?? user;
            var enableSsl = bool.Parse(_config["Email:Smtp:EnableSsl"] ?? "true");

            try
            {
                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Credentials = new NetworkCredential(user, pass),
                    Timeout = 10000
                };

                var mail = new MailMessage(from!, email, subject, htmlMessage)
                {
                    IsBodyHtml = true
                };

                await client.SendMailAsync(mail);
                _logger.LogInformation("📧 Email gönderildi. To={Email}, Subject={Subject}", email, subject);
            }
            catch (System.Exception ex)
            {
                // ❗ Artık kayıt akışını bozmaz, sadece loglanır
                _logger.LogError(ex, "E-posta gönderilemedi. To={Email}, Subject={Subject}", email, subject);
            }
        }
    }
}
