//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Options;
//using System.Net.Http;
//using System.Threading.Tasks;
//using AlisverisSitesiFinal.Services;

//namespace AlisverisSitesiFinal.Controllers
//{
//    [Authorize(Roles = "Admin")]
//    [Route("debug")]
//    public class DebugController : Controller
//    {
//        private readonly IHttpClientFactory _httpFactory;
//        private readonly IOptions<PayTROptions> _opt;

//        public DebugController(IHttpClientFactory httpFactory, IOptions<PayTROptions> opt)
//        {
//            _httpFactory = httpFactory;
//            _opt = opt;
//        }

//        // Bu endpoint sunucunun PAYTR'e giderken kullanacağı dış (outbound) IP'yi gösterir
//        [HttpGet("outbound-ip")]
//        public async Task<IActionResult> OutboundIp()
//        {
//            var http = _httpFactory.CreateClient();
//            var ip = await http.GetStringAsync("https://api.ipify.org"); // sadece düz string IP döner
//            return Content($"Outbound IP: {ip}");
//        }

//        // Yüklenen PayTR ayarlarını teyit edelim (gizli alanları maskeleyerek)
//        [HttpGet("paytr-config")]
//        public IActionResult PaytrConfig()
//        {
//            var v = _opt.Value;
//            string Mask(string s) => string.IsNullOrEmpty(s) ? "(empty)" :
//                s.Length <= 4 ? "****" : new string('*', s.Length - 4) + s.Substring(s.Length - 4);

//            return Content(
//                $"MerchantId={v.MerchantId}\n" +
//                $"MerchantKey={Mask(v.MerchantKey)}\n" +
//                $"MerchantSalt={Mask(v.MerchantSalt)}\n" +
//                $"BaseUrl={v.BaseUrl}\n" +
//                $"CallbackUrl={v.CallbackUrl}\n");
//        }
//    }
//}
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Options;
//using System.Net.Http;
//using System.Threading.Tasks;
//using AlisverisSitesiFinal.Services;

//namespace AlisverisSitesiFinal.Controllers
//{
//    [Authorize(Roles = "Admin")]
//    [Route("debug")]
//    public class DebugController : Controller
//    {
//        private readonly IHttpClientFactory _httpFactory;
//        private readonly IOptions<PayTROptions> _opt;

//        public DebugController(IHttpClientFactory httpFactory, IOptions<PayTROptions> opt)
//        {
//            _httpFactory = httpFactory;
//            _opt = opt;
//        }

//        [HttpGet("paytr-token-test")]
//        public async Task<IActionResult> PaytrTokenTest([FromServices] PayTRService svc)
//        {
//            var basket = PayTRService.BuildUserBasketBase64(new[] { ("Test Ürün", 1.23m, 1) });
//            var oid = "MARALINE" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

//            var (ok, msg, used) = await svc.GetIframeTokenAsync(
//                merchantOid: oid,
//                userIp: "94.199.202.131", // outbound IP
//                email: "test@maraline.com.tr",
//                paymentAmountKurus: 123,
//                userBasketBase64: basket,
//                noInstallment: 0,
//                maxInstallment: 12,
//                currency: "TL",
//                testMode: 1,
//                non3d: 0,
//                userName: "Test Kullanıcı",
//                userAddress: "Adres yok",
//                userPhone: "0000000000",
//                forceVariant: null // tüm varyantları sırayla dener
//            );

//            var header = ok ? "BASARILI" : "ARIZALI";
//            return Content($"{header} (variant={used})\n{msg}", "text/plain; charset=utf-8");
//        }
//        [HttpGet("paytr-build")]
//        public IActionResult PaytrBuild([FromServices] IOptions<PayTROptions> opt)
//        {
//            // Örnek sabitler
//            var userIp = "94.199.202.131"; var oid = "MARALINE" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
//            var email = "test@maraline.com.tr"; var kurus = 123; var noI = 0; var maxI = 12; var cur = "TL"; var test = 1; var n3d = 0;
//            var basket = AlisverisSitesiFinal.Services.PayTRService.BuildUserBasketBase64(new[] { ("Test", 1.23m, 1) });
//            var o = opt.Value;

//            string hashStr = string.Concat(o.MerchantId, userIp, oid, email, kurus.ToString(CultureInfo.InvariantCulture),
//                basket, noI.ToString(), maxI.ToString(), cur, test.ToString(), n3d.ToString());
//            string token;
//            using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(o.MerchantKey.Trim())))
//            {
//                token = Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(hashStr + o.MerchantSalt.Trim())));
//            }

//            string Mask(string s) => string.IsNullOrEmpty(s) ? "(empty)" : (s.Length <= 4 ? "****" : new string('*', s.Length - 4) + s[^4..]);
//            return Content(
//                $"merchant_id={o.MerchantId}\n" +
//                $"user_ip={userIp}\nmerchant_oid={oid}\nemail={email}\n" +
//                $"payment_amount={kurus}\nuser_basket(Base64)={basket}\n" +
//                $"no_installment={noI}\nmax_installment={maxI}\ncurrency={cur}\n" +
//                $"test_mode={test}\nnon_3d={n3d}\n" +
//                $"hash_str={hashStr}\n" +
//                $"paytr_token={token}\n" +
//                $"(MerchantKey={Mask(o.MerchantKey)}, MerchantSalt={Mask(o.MerchantSalt)})\n",
//                "text/plain; charset=utf-8");
//        }

//    }
//}

