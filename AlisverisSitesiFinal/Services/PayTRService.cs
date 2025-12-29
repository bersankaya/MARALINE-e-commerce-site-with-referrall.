using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AlisverisSitesiFinal.Services
{
   public class PayTROptions
    {
        public string MerchantId { get; set; } = "";
        public string MerchantKey { get; set; } = "";
        public string MerchantSalt { get; set; } = "";
        public string BaseUrl { get; set; } = "https://www.paytr.com";
        public string CallbackUrl { get; set; } = "";
        public string OkUrl { get; set; } = "";
        public string FailUrl { get; set; } = "";
    }

    public class PayTRService
    {
        private readonly PayTROptions _opt;
        private readonly HttpClient _http;

        public PayTRService(IOptions<PayTROptions> opt, IHttpClientFactory httpFactory)
        {
            _opt = opt.Value;
            _opt.MerchantId  = _opt.MerchantId?.Trim()  ?? "";
            _opt.MerchantKey = _opt.MerchantKey?.Trim() ?? "";
            _opt.MerchantSalt= _opt.MerchantSalt?.Trim()?? "";
            _opt.BaseUrl     = string.IsNullOrWhiteSpace(_opt.BaseUrl) ? "https://www.paytr.com" : _opt.BaseUrl.Trim();
            _http = httpFactory.CreateClient();
            _http.Timeout = TimeSpan.FromSeconds(30);
        }

        public static string BuildUserBasketBase64(IEnumerable<(string ad, decimal fiyatTl, int adet)> items)
        {
            var list = items.Select(x => new object[]
            {
                x.ad,
                x.fiyatTl.ToString("0.00", CultureInfo.InvariantCulture),
                x.adet
            }).ToList();
            var json = JsonSerializer.Serialize(list);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        private string BuildHashString(
            string userIp, string merchantOid, string email, int paymentAmountKurus,
            string userBasketBase64, int noInstallment, int maxInstallment, string currency, int testMode)
        {
            return string.Concat(
                _opt.MerchantId,
                userIp,
                merchantOid,
                email ?? "",
                paymentAmountKurus.ToString(CultureInfo.InvariantCulture),
                userBasketBase64,
                noInstallment.ToString(CultureInfo.InvariantCulture),
                maxInstallment.ToString(CultureInfo.InvariantCulture),
                currency,
                testMode.ToString(CultureInfo.InvariantCulture),
                _opt.MerchantSalt
            );
        }

        private string BuildToken(string hashStr)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opt.MerchantKey));
            var raw = hmac.ComputeHash(Encoding.UTF8.GetBytes(hashStr));
            return Convert.ToBase64String(raw);
        }

        private static string EnsureUrl(Uri baseFrom, string? url, string defaultPath)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                // Yanlışlıkla /Siparis bırakılmışsa otomatik düzelt
                if (url.EndsWith("/Siparis", StringComparison.OrdinalIgnoreCase))
                    return $"{baseFrom.Scheme}://{baseFrom.Host}{(baseFrom.IsDefaultPort ? "" : ":" + baseFrom.Port)}{defaultPath}";
                return url;
            }
            return $"{baseFrom.Scheme}://{baseFrom.Host}{(baseFrom.IsDefaultPort ? "" : ":" + baseFrom.Port)}{defaultPath}";
        }

        private FormUrlEncodedContent BuildForm(
            string token, string userIp, string merchantOid, string email, int paymentAmountKurus,
            string basketB64, int noInstallment, int maxInstallment, string currency,
            int testMode, int? non3d, string userName, string userAddress, string userPhone)
        {
            // Domaini Callback ya da OkUrl’den türet
            var baseUri = new Uri(!string.IsNullOrWhiteSpace(_opt.CallbackUrl)
                ? _opt.CallbackUrl
                : (!string.IsNullOrWhiteSpace(_opt.OkUrl) ? _opt.OkUrl : "https://maraline.com.tr/"));

            var okUrl   = EnsureUrl(baseUri, _opt.OkUrl,   "/odeme/ok-donus");
            var failUrl = EnsureUrl(baseUri, _opt.FailUrl, "/odeme/fail-donus");
            var cbUrl   = EnsureUrl(baseUri, _opt.CallbackUrl, "/odeme/paytr-callback");

            var pairs = new List<KeyValuePair<string, string>>
            {
                new("merchant_id", _opt.MerchantId),
                new("user_ip", userIp),
                new("merchant_oid", merchantOid),
                new("email", email ?? ""),
                new("user_name",    string.IsNullOrWhiteSpace(userName)    ? "musteri"    : userName),
                new("user_address", string.IsNullOrWhiteSpace(userAddress) ? "Adres yok"  : userAddress),
                new("user_phone",   string.IsNullOrWhiteSpace(userPhone)   ? "0000000000" : userPhone),
                new("payment_amount", paymentAmountKurus.ToString(CultureInfo.InvariantCulture)),
                new("user_basket", basketB64),
                new("no_installment",  noInstallment.ToString(CultureInfo.InvariantCulture)),
                new("max_installment", maxInstallment.ToString(CultureInfo.InvariantCulture)),
                new("currency", currency),
                new("test_mode", testMode.ToString(CultureInfo.InvariantCulture)),
                new("merchant_ok_url", okUrl),
                new("merchant_fail_url", failUrl),
                new("merchant_notify_url", cbUrl),
                new("paytr_token", token)
            };
            if (non3d.HasValue) pairs.Add(new("non_3d", non3d.Value.ToString(CultureInfo.InvariantCulture)));

            return new FormUrlEncodedContent(pairs);
        }

        public async Task<(bool ok, string tokenOrError)> GetIframeTokenAsync(
            string merchantOid, string userIp, string email, int paymentAmountKurus,
            string userBasketBase64, int noInstallment = 0, int maxInstallment = 0,
            string currency = "TL", int testMode = 0, int? non3d = null,
            string? userName = null, string? userAddress = null, string? userPhone = null)
        {
            var hashStr = BuildHashString(
                userIp, merchantOid, email, paymentAmountKurus,
                userBasketBase64, noInstallment, maxInstallment, currency, testMode);

            var paytrToken = BuildToken(hashStr);

            using var form = BuildForm(paytrToken, userIp, merchantOid, email, paymentAmountKurus,
                                       userBasketBase64, noInstallment, maxInstallment, currency,
                                       testMode, non3d, userName ?? "musteri", userAddress ?? "Adres yok", userPhone ?? "0000000000");

            var url = $"{_opt.BaseUrl.TrimEnd('/')}/odeme/api/get-token";
            var resp = await _http.PostAsync(url, form);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return (false, $"HTTP {(int)resp.StatusCode}: {body}");

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var st) &&
                    string.Equals(st.GetString(), "success", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, root.GetProperty("token").GetString() ?? "");
                }
                var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : "Bilinmeyen hata";
                return (false, reason ?? "Bilinmeyen hata");
            }
            catch
            {
                return (false, body);
            }
        }

        public bool VerifyCallbackHash(string merchantOid, string status, string totalAmount, string receivedHash)
        {
            var input = merchantOid + _opt.MerchantSalt + status + totalAmount;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opt.MerchantKey));
            var calc = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(input)));
            return string.Equals(calc, receivedHash, StringComparison.Ordinal);
        }

        // ======== İade ========

        // PayTR iade (TL üzerinden)
        public async Task<(bool ok, string message)> RefundAsyncTl(
            string merchantOid, decimal amountTl, string? reason = null, string? description = null)
        {
            // TL -> kuruş
            int returnAmount = (int)Math.Round(amountTl * 100m, MidpointRounding.AwayFromZero);

            // --- Zorunlu alanlar ---
            var pairs = new List<KeyValuePair<string, string>>
    {
        new("merchant_id",  _opt.MerchantId),
        new("merchant_oid", merchantOid),
        new("return_amount", returnAmount.ToString(CultureInfo.InvariantCulture)),
    };
            if (!string.IsNullOrWhiteSpace(reason)) pairs.Add(new("reason", reason));
            if (!string.IsNullOrWhiteSpace(description)) pairs.Add(new("description", description));

            // --- DOĞRU TOKEN HESABI ---
            // paytr_token = HMACSHA256( merchant_id + merchant_oid + return_amount + merchant_salt, merchant_key )
            var toSign = _opt.MerchantId
                         + merchantOid
                         + returnAmount.ToString(CultureInfo.InvariantCulture)
                         + _opt.MerchantSalt;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opt.MerchantKey));
            var token = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign)));
            pairs.Add(new("paytr_token", token));

            // --- İstek ---
            var url = $"{_opt.BaseUrl.TrimEnd('/')}/odeme/iade";
            var resp = await _http.PostAsync(url, new FormUrlEncodedContent(pairs));
            var body = await resp.Content.ReadAsStringAsync();

            // --- Yanıtı yorumla (HTTP 200 olsa bile gövdeye bak) ---
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;

                if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
                    return (true, "success");

                string reasonText =
                    (root.TryGetProperty("reason", out var r) ? r.GetString() : null)
                    ?? (root.TryGetProperty("err_msg", out var e) ? e.GetString() : null)
                    ?? body;

                return (false, reasonText!);
            }
            catch
            {
                if (resp.IsSuccessStatusCode) return (false, body);
                return (false, $"HTTP {(int)resp.StatusCode}: {body}");
            }
        }


    }
}
