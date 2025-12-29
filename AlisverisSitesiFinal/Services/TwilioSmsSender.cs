using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace AlisverisSitesiFinal.Services
{
    public class TwilioSmsSender : ISmsSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<TwilioSmsSender> _logger;
        private readonly string _from;

        public TwilioSmsSender(IConfiguration config, ILogger<TwilioSmsSender> logger)
        {
            _config = config;
            _logger = logger;

            var sid = _config["Sms:Twilio:AccountSid"];
            var token = _config["Sms:Twilio:AuthToken"];
            _from = _config["Sms:Twilio:From"] ?? throw new Exception("Sms:Twilio:From ayarlanmalı");

            TwilioClient.Init(sid, token);
        }

        public async Task SendSmsAsync(string number, string message)
        {
            try
            {
                var msg = await MessageResource.CreateAsync(
                    to: new Twilio.Types.PhoneNumber(number),
                    from: new Twilio.Types.PhoneNumber(_from),
                    body: message
                );
                _logger.LogInformation("📱 SMS sent → {To} | Sid={Sid}", number, msg.Sid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS gönderilemedi. To={Number}", number);
                throw;
            }
        }
    }
}
