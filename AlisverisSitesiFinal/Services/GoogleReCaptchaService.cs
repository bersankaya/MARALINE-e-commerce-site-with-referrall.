using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AlisverisSitesiFinal.Services
{
    public class GoogleReCaptchaSettings
    {
        public string SiteKey { get; set; } = "";
        public string SecretKey { get; set; } = "";
    }

    public class GoogleReCaptchaService
    {
        private readonly GoogleReCaptchaSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;

        public GoogleReCaptchaService(IOptions<GoogleReCaptchaSettings> settings,
                                      IHttpClientFactory httpClientFactory)
        {
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<bool> VerifyAsync(string token, double minimumScore = 0.5, string expectedAction = "login")
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            var client = _httpClientFactory.CreateClient();
            var resp = await client.PostAsync(
                $"https://www.google.com/recaptcha/api/siteverify?secret={_settings.SecretKey}&response={token}",
                null);

            var json = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ReCaptchaResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result?.Success == true
                   && result.Action == expectedAction
                   && result.Score >= minimumScore;
        }


        private sealed class ReCaptchaResponse
        {
            public bool Success { get; set; }
            public double Score { get; set; }
            public string Action { get; set; } = "";
        }
    }
}
