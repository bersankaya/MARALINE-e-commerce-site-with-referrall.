using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace AlisverisSitesiFinal.Controllers
{
    [Route("odeme")]
    [Authorize(Policy = "CanPurchase")]
    public class OdemeController : Controller
    {
        private readonly UygulamaDbContext _db;
        private readonly PayTRService _paytr;
        private readonly UserManager<Kullanici> _userManager;
        private readonly IOptions<ReferralConfig> _refConfig;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<OdemeController> _logger;
        private readonly IOrderNotificationService _orderNotify;
        private readonly IEmailSender _emailSender;
        public OdemeController(
            UygulamaDbContext db,
            PayTRService paytr,
            UserManager<Kullanici> userManager,
            IOptions<ReferralConfig> refConfig,
            IWebHostEnvironment env,
            ILogger<OdemeController> logger,
            IOrderNotificationService orderNotify,
            IEmailSender emailSender)
        {
            _db = db;
            _paytr = paytr;
            _userManager = userManager;
            _refConfig = refConfig;
            _env = env;
            _logger = logger;
            _orderNotify = orderNotify;
            _emailSender = emailSender;
        }

        // ==================== ÖDEME BAŞLAT ====================
        [HttpGet("baslat")]
        public async Task<IActionResult> Baslat()
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null)
                {
                    TempData["ErrorMessage"] = "Oturum bulunamadı.";
                    return RedirectToAction("Index", "Sepet");
                }

                var sepet = await _db.Sepet
                    .Include(s => s.Urun)
                    .Where(s => s.KullaniciId == me.Id)
                    .ToListAsync();

                if (!sepet.Any())
                {
                    TempData["ErrorMessage"] = "Sepetiniz boş.";
                    return RedirectToAction("Index", "Sepet");
                }

                var adres = await _db.Adresler
                    .Where(a => a.KullaniciId == me.Id)
                    .OrderByDescending(a => a.IsVarsayilan)
                    .FirstOrDefaultAsync();

                if (adres == null)
                {
                    TempData["ErrorMessage"] = "Ödeme için önce bir adres ekleyiniz.";
                    return RedirectToAction("Create", "Adresler", new { returnUrl = Url.Action("Index", "Sepet") });
                }

                decimal toplamTl = 0m;
                foreach (var i in sepet)
                {
                    if (i.Urun == null || !i.Urun.YayindaMi)
                    {
                        TempData["ErrorMessage"] = $"{i.Urun?.Ad ?? "Ürün"} şu an satışta değil.";
                        return RedirectToAction("Index", "Sepet");
                    }
                    if (i.Urun.StokAdedi < i.Miktar)
                    {
                        TempData["ErrorMessage"] = $"{i.Urun.Ad} stok yetersiz.";
                        return RedirectToAction("Index", "Sepet");
                    }
                    toplamTl += i.Urun.Fiyat * i.Miktar;
                }
                if (toplamTl <= 0)
                {
                    TempData["ErrorMessage"] = "Ödeme tutarı geçersiz.";
                    return RedirectToAction("Index", "Sepet");
                }

                // PayTR sepet (Base64 JSON)
                var basketLines = sepet.Select(x => (x.Urun!.Ad, x.Urun!.Fiyat, x.Miktar)).ToList();
                string basketB64 = PayTRService.BuildUserBasketBase64(basketLines);

                // merchant_oid (<=64)
                string user32 = (me.Id ?? Guid.NewGuid().ToString("N")).Replace("-", "");
                if (user32.Length > 32) user32 = user32[..32];
                else if (user32.Length < 32) user32 = user32.PadLeft(32, '0');

                string ts13 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                var rnd = new byte[4];
                System.Security.Cryptography.RandomNumberGenerator.Fill(rnd);
                string rand8 = BitConverter.ToString(rnd).Replace("-", "");
                string merchantOid = $"MARALINE{user32}{ts13}{rand8}";
                string snapOid = merchantOid.Length <= 64 ? merchantOid : merchantOid[..64];

                // IP
                string? userIp = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
                userIp ??= HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
                if (string.IsNullOrWhiteSpace(userIp)) userIp = "127.0.0.1";

                static string Trunc(string? s, int max) => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);
                string userName = !string.IsNullOrWhiteSpace(me.AdSoyad) ? me.AdSoyad : $"{me.Ad} {me.Soyad}".Trim();
                if (string.IsNullOrWhiteSpace(userName)) userName = me.UserName ?? "Müşteri";

                static string? TryGet(object obj, string propName)
                    => obj.GetType().GetProperty(propName)?.GetValue(obj)?.ToString();

                var addrParts = new List<string?>
                {
                    adres.AdresDetayi, adres.Ilce, adres.Il, adres.PostaKodu,
                    TryGet(adres,"Mahalle"), TryGet(adres,"Semt"), TryGet(adres,"Cadde"),
                    TryGet(adres,"Sokak"), TryGet(adres,"KapiNo")
                };
                string rawAddress = string.Join(" ", addrParts.Where(x => !string.IsNullOrWhiteSpace(x)));

                var adresTel = TryGet(adres, "Telefon");
                string userPhone = !string.IsNullOrWhiteSpace(adresTel) ? adresTel! : (!string.IsNullOrWhiteSpace(me.PhoneNumber) ? me.PhoneNumber! : "0000000000");

                var snapUserName = Trunc(userName, 128);
                var snapUserPhone = Trunc(userPhone, 32);
                var snapUserAddr = Trunc(string.IsNullOrWhiteSpace(rawAddress) ? "Adres yok" : rawAddress, 1024);
                var snapEmail = Trunc(me.Email ?? $"{me.UserName}@example.com", 256);
                var snapUserId = Trunc(me.Id ?? "", 64);

                int paymentAmountKurus = (int)Math.Round(toplamTl * 100m, MidpointRounding.AwayFromZero);

                // Canlıda 0 olmalı; istersen sabitleyebilirsin
                int testMode = 0;

                var tokenResp = await _paytr.GetIframeTokenAsync(
                    merchantOid: snapOid,
                    userIp: userIp!,
                    email: snapEmail,
                    paymentAmountKurus: paymentAmountKurus,
                    userBasketBase64: basketB64,
                    noInstallment: 0,
                    maxInstallment: 12,
                    currency: "TL",
                    testMode: testMode,
                    non3d: 0,
                    userName: snapUserName,
                    userAddress: snapUserAddr,
                    userPhone: userPhone
                );

                if (!tokenResp.ok)
                {
                    TempData["ErrorMessage"] = "Ödeme başlatılamadı: " + tokenResp.tokenOrError;
                    _logger.LogWarning("PayTR token hatası: {Reason} oid={Oid} , PayTR tarafında geçici bir teknik sorun oluştu. Lütfen birkaç dakika sonra tekrar deneyin.", tokenResp.tokenOrError, snapOid);
                    return RedirectToAction("Index", "Sepet");
                }

                // --- SNAPSHOT: sadece modeldeki varyasyon alanlarını ekle ---
                var snapItems = sepet.Select(s => new
                {
                    urunId = s.UrunId,
                    miktar = s.Miktar,
                    birim = s.Urun!.Fiyat,
                    s.Renk,
                    s.Beden,
                    s.Boyut,
                    s.Numara,
                    s.Kapasite,
                    s.Materyal,
                    s.Desen,
                    s.Aciklama
                }).ToList();

                var snap = new OdemeBeklet
                {
                    MerchantOid = snapOid,
                    KullaniciId = snapUserId,
                    Email = snapEmail,
                    UserName = snapUserName,
                    UserPhone = userPhone,
                    UserAddress = snapUserAddr,
                    ToplamTutar = toplamTl,
                    SepetJson = JsonSerializer.Serialize(snapItems),
                    Olusturma = DateTime.UtcNow
                };

                _db.OdemeBekletler.Add(snap);
                await _db.SaveChangesAsync();
                _logger.LogInformation("OdemeBeklet kaydedildi. oid={Oid} user={User}", snapOid, snapUserId);

                ViewBag.IframeToken = tokenResp.tokenOrError;
                ViewBag.MerchantOid = snapOid;
                return View("Iframe");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ödeme adımı başlatılırken bir hata oluştu: " + ex.Message;
                _logger.LogError(ex, "Baslat genel hata");
                return RedirectToAction("Index", "Sepet");
            }
        }

        // ==================== PAYTR CALLBACK: POST ve GET ====================
        [AllowAnonymous, IgnoreAntiforgeryToken]
        [HttpPost("paytr-callback")]
        [HttpPost("paytr_callback")]
        [HttpPost("notify")]
        public Task<IActionResult> PaytrCallbackPost() => HandlePaytrCallbackAsync();

        [AllowAnonymous, IgnoreAntiforgeryToken]
        [HttpGet("paytr-callback")]
        [HttpGet("paytr_callback")]
        [HttpGet("notify")]
        public Task<IActionResult> PaytrCallbackGet() => HandlePaytrCallbackAsync();

        // ==================== OK DÖNÜŞ (garanti) ====================
        [AllowAnonymous, IgnoreAntiforgeryToken]
        [HttpGet("ok-donus")]
        public Task<IActionResult> OkDonus() => HandlePaytrCallbackAsync(fromOkReturn: true);

        [AllowAnonymous, IgnoreAntiforgeryToken]
        [HttpGet("fail-donus")]
        public IActionResult FailDonus()
        {
            return RedirectToAction("Index", "Sepet");
        }

        // ==================== ORTAK İŞLEYİCİ ====================
        private async Task<IActionResult> HandlePaytrCallbackAsync(bool fromOkReturn = false)
        {
            try
            {
                IFormCollection? form = null;
                string raw = "";
                try { form = await Request.ReadFormAsync(); } catch { }

                if (form == null || !form.Keys.Any())
                {
                    try
                    {
                        Request.EnableBuffering();
                        using var sr = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                        raw = await sr.ReadToEndAsync();
                        Request.Body.Position = 0;

                        var dict = System.Web.HttpUtility.ParseQueryString(raw);
                        var pairs = dict.AllKeys.Where(k => k != null)
                            .ToDictionary(k => k!, k => dict[k!] ?? "");
                        form = new FormCollection(pairs.ToDictionary(kv => kv.Key, kv => new Microsoft.Extensions.Primitives.StringValues(kv.Value)));
                    }
                    catch { }
                }

                string Get(string key)
                {
                    if (form != null)
                    {
                        if (form.TryGetValue(key, out var v)) return v.ToString();
                        if (form.TryGetValue(key.ToLowerInvariant(), out var v2)) return v2.ToString();
                    }
                    var q = Request.Query[key];
                    if (!string.IsNullOrEmpty(q)) return q.ToString();
                    if (!string.IsNullOrEmpty(raw))
                    {
                        var val = System.Web.HttpUtility.ParseQueryString(raw)[key];
                        if (!string.IsNullOrEmpty(val)) return val!;
                    }
                    return "";
                }

                var oidRaw = Get("merchant_oid");
                var status = Get("status");
                var total = Get("total_amount");
                var hash = Get("hash");
                var testMode = Get("test_mode");
                string oid = (oidRaw ?? "").Trim();

                bool isTest = testMode == "1" || testMode.Equals("true", StringComparison.OrdinalIgnoreCase);

                if (!fromOkReturn)
                {
                    bool isValid = _paytr.VerifyCallbackHash(oid, status, total, hash);
                    if (!isValid && !isTest) return Content("OK");
                    if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)) return Content("OK");
                }

                // idempotent kontrol (OrderRefKey kolonun varsa)
                var pRefCheck = typeof(Siparis).GetProperty("OrderRefKey");
                if (pRefCheck != null && await _db.Siparisler.AnyAsync(s => EF.Property<string>(s, "OrderRefKey") == oid))
                    return fromOkReturn ? RedirectToAction("Index", "Siparis") : Content("OK");

                await CreateOrderFromSnapshotAsync(oid);

                return fromOkReturn ? RedirectToAction("Index", "Siparis") : Content("OK");
            }
            catch (Exception exOuter)
            {
                _logger?.LogError(exOuter, "Callback/OkDonus error");
                return fromOkReturn ? RedirectToAction("Index", "Siparis") : Content("OK");
            }
        }


        // ==================== SİPARİŞ OLUŞTURMA ====================
        private async Task<bool> CreateOrderFromSnapshotAsync(string oid)
        {
            try
            {
                OdemeBeklet? snap = await _db.OdemeBekletler.FirstOrDefaultAsync(x => x.MerchantOid == oid);
                if (snap == null)
                {
                    string o32 = oid.Length >= 32 ? oid[..32] : oid;
                    string o48 = oid.Length >= 48 ? oid[..48] : oid;
                    string o64 = oid.Length >= 64 ? oid[..64] : oid;

                    var adaylar = await _db.OdemeBekletler
                        .Where(x =>
                            x.MerchantOid == o64 ||
                            x.MerchantOid.StartsWith(o64) || o64.StartsWith(x.MerchantOid) ||
                            x.MerchantOid.StartsWith(o48) || x.MerchantOid.StartsWith(o32) ||
                            o48.StartsWith(x.MerchantOid) || o32.StartsWith(x.MerchantOid))
                        .OrderByDescending(x => x.Olusturma)
                        .Take(10)
                        .ToListAsync();

                    var limit = DateTime.UtcNow.AddMinutes(-60);
                    snap = adaylar.Where(a => a.Olusturma >= limit).OrderByDescending(a => a.Olusturma).FirstOrDefault();

                    if (snap == null) return false;
                }

                // FK için ilk ürün
                int? firstUrunIdForFk = null;
                try
                {
                    using var doc = JsonDocument.Parse(snap.SepetJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            if (el.TryGetProperty("urunId", out var rU))
                            {
                                firstUrunIdForFk = rU.GetInt32();
                                break;
                            }
                            else if (el.TryGetProperty("UrunId", out var rU2))
                            {
                                firstUrunIdForFk = rU2.GetInt32();
                                break;
                            }
                        }
                    }
                }
                catch { }

                // Kalemler
                List<JsonElement>? items = null;
                try { items = JsonSerializer.Deserialize<List<JsonElement>>(snap.SepetJson); } catch { }
                items ??= new List<JsonElement>();

                using var trx = await _db.Database.BeginTransactionAsync();
                try
                {
                    var siparis = new Siparis
                    {
                        IdentityUserId = snap.KullaniciId,
                        SiparisTarihi = DateTime.Now,
                        Durum = "Beklemede",
                        ToplamTutar = 0m
                    };

                    if (firstUrunIdForFk.HasValue) siparis.UrunId = firstUrunIdForFk.Value;

                    // OID'yi kaydet (OrderRefKey varsa oraya, yoksa IyzicoPaymentId'ye)
                    var pRef = typeof(Siparis).GetProperty("OrderRefKey");
                    if (pRef != null) pRef.SetValue(siparis, oid);
                    var pIyz = typeof(Siparis).GetProperty("IyzicoPaymentId");
                    if (pIyz != null && (pRef == null || string.IsNullOrWhiteSpace(pRef.GetValue(siparis)?.ToString())))
                        pIyz.SetValue(siparis, oid);

                    // Adres: string alanlar
                    string ship = string.IsNullOrWhiteSpace(snap.UserAddress) ? "Adres yok" : snap.UserAddress;
                    string[] stringAddrProps = { "TeslimatAdresi", "AdresMetni", "AdresText", "GonderimAdresi" };
                    foreach (var name in stringAddrProps)
                    {
                        var pi = typeof(Siparis).GetProperty(name);
                        if (pi != null && pi.CanWrite && pi.PropertyType == typeof(string))
                            pi.SetValue(siparis, ship);
                    }

                    // Adres: Id alanları
                    int? defaultAdresId = null;
                    try
                    {
                        var a = await _db.Adresler
                            .Where(x => x.KullaniciId == snap.KullaniciId)
                            .OrderByDescending(x => x.IsVarsayilan)
                            .FirstOrDefaultAsync();
                        if (a != null) defaultAdresId = a.Id;
                    }
                    catch { }

                    string[] intAddrIdProps = { "AdresId", "TeslimatAdresId", "GonderimAdresId" };
                    foreach (var name in intAddrIdProps)
                    {
                        var pi = typeof(Siparis).GetProperty(name);
                        if (pi != null && pi.CanWrite &&
                            (pi.PropertyType == typeof(int) || pi.PropertyType == typeof(int?)) &&
                            defaultAdresId.HasValue)
                        {
                            pi.SetValue(siparis, defaultAdresId.Value);
                        }
                    }

                    _db.Siparisler.Add(siparis);
                    await _db.SaveChangesAsync();

                    // ---- CASE-INSENSITIVE JSON OKUMA YARDIMCILARI ----
                    static int GetInt(JsonElement el, params string[] names)
                    {
                        foreach (var n in names)
                            if (el.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
                        return 0;
                    }
                    static decimal GetDecimal(JsonElement el, params string[] names)
                    {
                        foreach (var n in names)
                            if (el.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
                        return 0m;
                    }
                    static string? GetString(JsonElement el, params string[] names)
                    {
                        foreach (var n in names)
                            if (el.TryGetProperty(n, out var v) && v.ValueKind != JsonValueKind.Null && v.ValueKind != JsonValueKind.Undefined)
                                return v.GetString();
                        return null;
                    }

                    foreach (var it in items)
                    {
                        // Hem küçük hem büyük harf isimlerini dene
                        int urunId = GetInt(it, "urunId", "UrunId");
                        int miktar = GetInt(it, "miktar", "Miktar");
                        decimal birim = GetDecimal(it, "birim", "Birim", "BirimFiyat");

                        if (urunId <= 0 || miktar <= 0) continue;

                        string? renk = GetString(it, "renk", "Renk");
                        string? beden = GetString(it, "beden", "Beden");
                        string? boyut = GetString(it, "boyut", "Boyut");
                        string? numara = GetString(it, "numara", "Numara");
                        string? kapasite = GetString(it, "kapasite", "Kapasite");
                        string? materyal = GetString(it, "materyal", "Materyal");
                        string? desen = GetString(it, "desen", "Desen");
                        string? aciklama = GetString(it, "aciklama", "Aciklama", "Açiklama", "Açıklama");

                        var urun = await _db.Uruns.FirstOrDefaultAsync(x => x.Id == urunId);
                        if (urun == null) continue;

                        _db.SiparisKalemleri.Add(new SiparisKalemi
                        {
                            Siparis = siparis,
                            UrunId = urun.Id,
                            Miktar = miktar,
                            BirimFiyat = birim,
                            SaticiId = urun.UserId ?? string.Empty,
                            SaticiTeklifAnlik = urun.SaticiTeklifFiyati,
                            AdminFiyatAnlik = urun.FiyatAdmin,
                            RefFiyatAnlik = urun.FiyatReferansli,
                            Renk = string.IsNullOrWhiteSpace(renk) ? null : renk,
                            Beden = string.IsNullOrWhiteSpace(beden) ? null : beden,
                            Boyut = string.IsNullOrWhiteSpace(boyut) ? null : boyut,
                            Numara = string.IsNullOrWhiteSpace(numara) ? null : numara,
                            Kapasite = string.IsNullOrWhiteSpace(kapasite) ? null : kapasite,
                            Materyal = string.IsNullOrWhiteSpace(materyal) ? null : materyal,
                            Desen = string.IsNullOrWhiteSpace(desen) ? null : desen,
                            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama
                        });

                        siparis.ToplamTutar += birim * miktar;
                    }

                    // Sepeti temizle
                    var cart = await _db.Sepet
                        .Where(s => s.KullaniciId == snap.KullaniciId ||
                                    s.KullaniciId.Replace("-", "") == (snap.KullaniciId ?? "").Replace("-", ""))
                        .ToListAsync();
                    if (cart.Any()) _db.Sepet.RemoveRange(cart);

                    // Snapshot sil
                    _db.OdemeBekletler.Remove(snap);

                    await _db.SaveChangesAsync();
                    await trx.CommitAsync();

                    _logger?.LogInformation("Order created. oid={oid}, siparisId={sid}", oid, siparis.Id);
                    // 🔔 Satıcıya bildirim e-postası gönder
                    try
                    {
                        await _orderNotify.NotifySellersAsync(siparis.Id);
                    }
                    catch (Exception mailEx)
                    {
                        _logger?.LogError(mailEx, "Sipariş bildirimi gönderilemedi. siparisId={sid}", siparis.Id);
                    }
                    try
                    {
                        // müşteri
                        var musteri = string.IsNullOrWhiteSpace(siparis.IdentityUserId)
                            ? null
                            : await _db.Users
                                .AsNoTracking()
                                .FirstOrDefaultAsync(u => u.Id == siparis.IdentityUserId);

                        if (musteri?.Email != null)
                        {
                            // sipariş kalemleri
                            var kalemler = await _db.SiparisKalemleri
                                .AsNoTracking()
                                .Include(k => k.Urun)
                                .Where(k => k.SiparisId == siparis.Id)
                                .ToListAsync();

                            var tr = CultureInfo.GetCultureInfo("tr-TR");
                            var sb = new StringBuilder();
                            sb.AppendLine("<div style='font-family:Arial,sans-serif;font-size:14px;color:#111'>");
                            sb.AppendLine("<h2 style='margin:0 0 10px'>Siparişiniz Alındı</h2>");
                            sb.AppendLine($"<p>Merhaba {(musteri.Ad + " " + musteri.Soyad).Trim()},</p>");
                            sb.AppendLine("<p>Siparişiniz başarıyla oluşturuldu. Detaylar aşağıdadır:</p>");
                            sb.AppendLine("<table cellpadding='6' cellspacing='0' style='border-collapse:collapse'>");
                            sb.AppendLine("<thead><tr><th align='left'>Ürün</th><th align='right'>Adet</th><th align='right'>Birim Fiyat</th></tr></thead><tbody>");
                            foreach (var k in kalemler)
                            {
                                var ad = k.Urun?.Ad ?? "Ürün";
                                sb.AppendLine("<tr>");
                                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(ad)}</td>");
                                sb.AppendLine($"<td align='right'>{k.Miktar}</td>");
                                sb.AppendLine($"<td align='right'>{k.BirimFiyat.ToString("C2", tr)}</td>");
                                sb.AppendLine("</tr>");
                            }
                            sb.AppendLine("</tbody></table>");
                            sb.AppendLine("<hr style='border:none;border-top:1px solid #eee;margin:12px 0'/>");
                            sb.AppendLine($"<p><strong>Sipariş No:</strong> {siparis.Id}<br/>");
                            sb.AppendLine($"<strong>Tarih:</strong> {siparis.SiparisTarihi:dd.MM.yyyy HH:mm}<br/>");
                            sb.AppendLine($"<strong>Toplam Tutar:</strong> {siparis.ToplamTutar.ToString("C2", tr)}</p>");
                            sb.AppendLine("<p>Maraline hesabınızdan siparişinizi görüntüleyebilirsiniz.</p>");
                            sb.AppendLine("</div>");

                            await _emailSender.SendEmailAsync(
                                musteri.Email,
                                $"[Maraline] Siparişiniz Alındı #{siparis.Id}",
                                sb.ToString()
                            );
                        }
                        else
                        {
                            _logger?.LogWarning("Müşteri e-postası bulunamadı. siparisId={sid}", siparis.Id);
                        }
                    }
                    catch (Exception ex2)
                    {
                        _logger?.LogError(ex2, "Müşteri sipariş onay maili başarısız. siparisId={sid}", siparis.Id);
                    }
                    return true;
                }
                catch
                {
                    await trx.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CreateOrderFromSnapshot error oid={oid}", oid);
                return false;
            }
        }
    }
}
