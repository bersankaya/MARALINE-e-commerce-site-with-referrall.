using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AlisverisSitesiFinal.Services
{
    public class NoopEmailSender : IEmailSender
    {
        private readonly ILogger<NoopEmailSender> _logger;
        public NoopEmailSender(ILogger<NoopEmailSender> logger) => _logger = logger;

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _logger.LogInformation("🧪 [NOOP] E-posta gönderimi atlandı. To={Email}, Subject={Subject}", email, subject);
            return Task.CompletedTask;
        }
    }
}
