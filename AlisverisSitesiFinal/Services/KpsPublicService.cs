// Path: Services/KpsPublicService.cs
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace AlisverisSitesiFinal.Services
{
    // KPS ayarları (appsettings: "KPS" bölümü)
    public sealed class KpsOptions
    {
        public string Endpoint { get; set; } = "";
        // KPS sonucu "false" gelirse kaydı blokla mı? (Prod’da geçici olarak false)
        public bool Enforce { get; set; } = true;
    }

    public interface IKpsPublicService
    {
        Task<bool> VerifyAsync(long tckn, string ad, string soyad, int dogumYili, CancellationToken ct = default);
    }

    /// <summary>
    /// NVİ KPS Public SOAP doğrulaması için typed HttpClient servisi
    /// </summary>
    public sealed class KpsPublicService : IKpsPublicService
    {
        private readonly HttpClient _http;
        private readonly string _endpoint;

        public KpsPublicService(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _endpoint = cfg["KPS:Endpoint"] ?? throw new Exception("KPS:Endpoint appsettings.json içinde tanımlı değil.");
        }

        public async Task<bool> VerifyAsync(long tckn, string ad, string soyad, int dogumYili, CancellationToken ct = default)
        {
            string NormalizeStrict(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                s = s.Trim();
                s = Regex.Replace(s, @"\s+", " ");                 // Çoklu boşlukları teke indir
                s = Regex.Replace(s, @"[^\p{L}\p{M}\s\-]", "");    // Harf, boşluk ve tire kalsın
                return s.ToUpper(new CultureInfo("tr-TR"));        // TR büyük harf
            }

            string ToAscii(string s) =>
                s.Replace('Ç', 'C').Replace('Ğ', 'G').Replace('İ', 'I')
                 .Replace('Ö', 'O').Replace('Ş', 'S').Replace('Ü', 'U')
                 .Replace('Â', 'A').Replace('Ê', 'E').Replace('Î', 'I')
                 .Replace('Ô', 'O').Replace('Û', 'U');

            var adStrict = NormalizeStrict(ad);
            var soyadStrict = NormalizeStrict(soyad);

            IEnumerable<(string ad, string soyad, bool ascii)> BuildCandidates()
            {
                yield return (adStrict, soyadStrict, false);
                yield return (ToAscii(adStrict), ToAscii(soyadStrict), true);

                var adParts = adStrict.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                var soyParts = soyadStrict.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

                if (adParts.Length > 1)
                {
                    var adFirst = adParts[0];
                    yield return (adFirst, soyadStrict, false);
                    yield return (ToAscii(adFirst), ToAscii(soyadStrict), true);
                }

                if (soyParts.Length > 1)
                {
                    var soyLast = soyParts[^1];
                    yield return (adStrict, soyLast, false);
                    yield return (ToAscii(adStrict), ToAscii(soyLast), true);
                }

                if (adParts.Length > 1 || soyParts.Length > 1)
                {
                    var adFirst = adParts.Length > 0 ? adParts[0] : adStrict;
                    var soyLast = soyParts.Length > 0 ? soyParts[^1] : soyadStrict;
                    yield return (adFirst, soyLast, false);
                    yield return (ToAscii(adFirst), ToAscii(soyLast), true);
                }
            }

            // ---- SOAP 1.1
            async Task<bool> PostSoap11Async(string adX, string soyadX)
            {
                var envelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <TCKimlikNoDogrula xmlns=""http://tckimlik.nvi.gov.tr/WS"">
      <TCKimlikNo>{tckn}</TCKimlikNo>
      <Ad>{adX}</Ad>
      <Soyad>{soyadX}</Soyad>
      <DogumYili>{dogumYili}</DogumYili>
    </TCKimlikNoDogrula>
  </soap:Body>
</soap:Envelope>";

                using var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
                content.Headers.ContentType!.CharSet = "utf-8";

                using var msg = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                {
                    Content = content
                };
                // Quoted SOAPAction (bazı sunucular bunu bekler)
                msg.Headers.TryAddWithoutValidation("SOAPAction", "\"http://tckimlik.nvi.gov.tr/WS/TCKimlikNoDogrula\"");
                msg.Headers.ExpectContinue = false;

                using var resp = await _http.SendAsync(msg, ct);
                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"KPS HTTP {(int)resp.StatusCode}");

                var xml = await resp.Content.ReadAsStringAsync(ct);
                return Regex.IsMatch(xml, @"<TCKimlikNoDogrulaResult>\s*true\s*</TCKimlikNoDogrulaResult>", RegexOptions.IgnoreCase);
            }

            // ---- SOAP 1.2 (Fallback)
            async Task<bool> PostSoap12Async(string adX, string soyadX)
            {
                var envelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
                 xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
                 xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
  <soap12:Body>
    <TCKimlikNoDogrula xmlns=""http://tckimlik.nvi.gov.tr/WS"">
      <TCKimlikNo>{tckn}</TCKimlikNo>
      <Ad>{adX}</Ad>
      <Soyad>{soyadX}</Soyad>
      <DogumYili>{dogumYili}</DogumYili>
    </TCKimlikNoDogrula>
  </soap12:Body>
</soap12:Envelope>";

                using var content = new StringContent(envelope, Encoding.UTF8, "application/soap+xml");
                content.Headers.ContentType!.CharSet = "utf-8";
                content.Headers.ContentType!.Parameters.Add(
                    new System.Net.Http.Headers.NameValueHeaderValue("action", "\"http://tckimlik.nvi.gov.tr/WS/TCKimlikNoDogrula\""));

                using var msg = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
                msg.Headers.ExpectContinue = false;

                using var resp = await _http.SendAsync(msg, ct);
                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"KPS HTTP {(int)resp.StatusCode}");

                var xml = await resp.Content.ReadAsStringAsync(ct);
                return Regex.IsMatch(xml, @"<TCKimlikNoDogrulaResult>\s*true\s*</TCKimlikNoDogrulaResult>", RegexOptions.IgnoreCase);
            }

            try
            {
                foreach (var cand in BuildCandidates())
                    if (await PostSoap11Async(cand.ad, cand.soyad)) return true;

                foreach (var cand in BuildCandidates())
                    if (await PostSoap12Async(cand.ad, cand.soyad)) return true;

                return false;
            }
            catch (TaskCanceledException)
            {
                throw new Exception("KPS servisine şu anda ulaşılamıyor (zaman aşımı).");
            }
        }
    }
}
