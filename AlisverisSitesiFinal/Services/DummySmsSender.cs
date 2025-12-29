using Microsoft.Extensions.Logging;

namespace AlisverisSitesiFinal.Services
{
    public class DummySmsSender : ISmsSender
    {
        private readonly ILogger<DummySmsSender> _logger;
        public DummySmsSender(ILogger<DummySmsSender> logger) => _logger = logger;

        public Task SendSmsAsync(string number, string message)
        {
            _logger.LogInformation("🧪 SMS (dummy) → {Number} | {Message}", number, message);
            return Task.CompletedTask;
        }
    }
}
