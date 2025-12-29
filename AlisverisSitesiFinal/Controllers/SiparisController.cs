using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize]
    public class SiparisController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;
        private readonly IOptions<ReferralConfig> _config;
        private const int IADE_SURESI_GUN = 14; // yasal cayma hakkı süresi
        private readonly InvoiceService _invoiceService;
        private readonly InvoicePdfService _invoicePdf;
        private readonly IEmailSender _emailSender;

        public SiparisController(
            UygulamaDbContext context,
            UserManager<Kullanici> userManager,
            IOptions<ReferralConfig> config,
            InvoiceService invoiceService,
            InvoicePdfService invoicePdf, IEmailSender emailSender)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
            _invoiceService = invoiceService;
            _invoicePdf = invoicePdf;
            _emailSender = emailSender;
        }

        // =======================
        // SATICI / ADMIN: TÜM SİPARİŞLER
        // =======================
        [Authorize(Roles = "Satici,Admin")]
        public async Task<IActionResult> TumSiparisler()
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);

                IQueryable<Siparis> q = _context.Siparisler
                    .AsNoTracking()
                    .Include(s => s.SiparisKalemleri).ThenInclude(k => k.Urun);

                // Satıcıysa sadece kendi mağazasının siparişleri
                if (User.IsInRole("Satici") && !User.IsInRole("Admin"))
                {
                    if (me == null) return View("Index", new List<Siparis>());

                    var myStore = await _context.Magazalar
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.OwnerUserId == me.Id);

                    if (myStore != null)
                    {
                        q = q.Where(s =>
                            s.SiparisKalemleri != null &&
                            s.SiparisKalemleri.Any(k => k.Urun != null && k.Urun.StoreId == myStore.Id));
                    }
                    else
                    {
                        return View("Index", new List<Siparis>());
                    }
                }

                var siparisler = await q.OrderByDescending(s => s.SiparisTarihi).ToListAsync();

                // İade talebi sözlüğü (View tarafı bekliyor)
                var sipIds = siparisler.Select(s => s.Id).ToList();
                var talepler = await _context.IadeTalepleri
                    .AsNoTracking()
                    .Where(t => sipIds.Contains(t.SiparisId))
                    .OrderByDescending(t => t.TalepTarihi)
                    .ToListAsync();

                ViewBag.Iadeler = talepler
                    .GroupBy(t => t.SiparisId)
                    .ToDictionary(g => g.Key, g => g.First());

                ViewBag.IadeSureGun = IADE_SURESI_GUN;

                // Aynı görünümü tekrar kullan: Index.cshtml
                return View("Index", siparisler);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Siparişler yüklenemedi: {ex.Message}";
                return View("Index", new List<Siparis>());
            }
        }

        // =======================
        // Müşterinin kendi siparişleri
        // =======================
        public async Task<IActionResult> Index()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Index", "Home");

                var siparisler = await _context.Siparisler
                    .AsNoTracking()
                    .Include(s => s.SiparisKalemleri).ThenInclude(k => k.Urun)
                    .Where(s => s.IdentityUserId == user.Id)
                    .OrderByDescending(s => s.SiparisTarihi)
                    .ToListAsync();

                var sipIds = siparisler.Select(s => s.Id).ToList();

                var talepler = await _context.IadeTalepleri
                    .AsNoTracking()
                    .Where(t => sipIds.Contains(t.SiparisId) && t.KullaniciId == user.Id)
                    .OrderByDescending(t => t.TalepTarihi)
                    .ToListAsync();

                var dic = new Dictionary<int, IadeTalebi>();
                foreach (var t in talepler)
                    if (!dic.ContainsKey(t.SiparisId))
                        dic[t.SiparisId] = t;

                ViewBag.Iadeler = dic;
                ViewBag.IadeSureGun = IADE_SURESI_GUN;

                return View(siparisler);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Siparişleriniz yüklenemedi: {ex.Message}";
                return View(new List<Siparis>());
            }
        }

        // Sepet üzerinden SATIN AL (ÖDEME ÖNCESİ: tetik yok, sipariş yok)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CanPurchase")]
        public async Task<IActionResult> SatinAl(
            int urunId,
            int miktar,
            string? renk = null, string? beden = null, string? boyut = null,
            string? numara = null, string? kapasite = null, string? materyal = null, string? desen = null)
        {
            if (User.IsInRole("Satici"))
            {
                TempData["ErrorMessage"] = "Satıcı hesapları alışveriş yapamaz.";
                return RedirectToAction("Index", "Home");
            }

            var kullanici = await _userManager.GetUserAsync(User);
            var urun = await _context.Uruns.FindAsync(urunId);
            if (kullanici == null || urun == null || miktar < 1 || urun.StokAdedi < miktar)
                return RedirectToAction("Index", "Uruns");

            // zorunlu varyasyon kontrolleri (mevcut yapınla aynı)
            if (urun.RenkSecimiVar && string.IsNullOrWhiteSpace(renk)) { TempData["ErrorMessage"] = "Lütfen renk seçiniz."; return RedirectToAction("Details", "Uruns", new { id = urun.Id }); }
            if (urun.BedenSecimiVar && string.IsNullOrWhiteSpace(beden)) { TempData["ErrorMessage"] = "Lütfen beden seçiniz."; return RedirectToAction("Details", "Uruns", new { id = urun.Id }); }
            if (urun.BoyutSecimiVar && string.IsNullOrWhiteSpace(boyut)) { TempData["ErrorMessage"] = "Lütfen boyut seçiniz."; return RedirectToAction("Details", "Uruns", new { id = urun.Id }); }
            if (urun.NumaraSecimiVar && string.IsNullOrWhiteSpace(numara)) { TempData["ErrorMessage"] = "Lütfen numara seçiniz."; return RedirectToAction("Details", "Uruns", new { id = urun.Id }); }
            if (urun.KapasiteSecimiVar && string.IsNullOrWhiteSpace(kapasite)) { TempData["ErrorMessage"] = "Lütfen kapasite seçiniz."; return RedirectToAction("Details", "Uruns", new { id = urun.Id }); }

            // SEPETE EKLE (aynı varyasyon varsa miktarı artır)
            var mevcut = await _context.Sepet.FirstOrDefaultAsync(s =>
                s.KullaniciId == kullanici.Id && s.UrunId == urun.Id &&
                s.Renk == renk && s.Beden == beden && s.Boyut == boyut &&
                s.Numara == numara && s.Kapasite == kapasite && s.Materyal == materyal && s.Desen == desen);

            if (mevcut != null)
            {
                mevcut.Miktar += miktar;
                _context.Sepet.Update(mevcut);
            }
            else
            {
                _context.Sepet.Add(new SepetKalemi
                {
                    KullaniciId = kullanici.Id,
                    UrunId = urun.Id,
                    Miktar = miktar,
                    Renk = renk,
                    Beden = beden,
                    Boyut = boyut,
                    Numara = numara,
                    Kapasite = kapasite,
                    Materyal = materyal,
                    Desen = desen,
                    Aciklama = $"Renk:{renk ?? "-"} | Beden:{beden ?? "-"} | Boyut:{boyut ?? "-"} | Numara:{numara ?? "-"} | Kapasite:{kapasite ?? "-"} | Materyal:{materyal ?? "-"} | Desen:{desen ?? "-"}"
                });
            }

            await _context.SaveChangesAsync();

            TempData["SiparisDurum"] = "Ödeme adımına geçiliyor…";
            return RedirectToAction("Baslat", "Odeme");
        }


        // =======================
        // SATICI / ADMIN: AKIŞ Aksiyonları
        // =======================
        [HttpPost, Authorize(Roles = "Satici,Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Onayla(int id)
        {
            var me = await _userManager.GetUserAsync(User);

            var s = await _context.Siparisler
                .Include(x => x.SiparisKalemleri).ThenInclude(k => k.Urun)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (s == null) return NotFound();
            if (!User.IsInRole("Admin") && !await KullaniciBuSiparisiYonetebilirMi(me, s)) return Forbid();

            // 1) Durumu güncelle
            s.Durum = "Onaylandı";
            await _context.SaveChangesAsync();

            // 2) Bonus (referral) etkilerini uygula (idempotent)
            var bonusHandler = new ReferralBonusHandler(_context, _userManager, _config);
            await bonusHandler.ApplyOrderEffectsAsync(s);

            // 3) HİZMET BEDELİ = dağıtılan bonus toplamı (fallback: config BonusMiktari)
            decimal hizmetBedeliToplam = 0m;
            try
            {
                if (!string.IsNullOrWhiteSpace(s.OrderRefKey))
                    hizmetBedeliToplam = await bonusHandler.GetOrderDistributedBonusTotalAsync(s.OrderRefKey);
            }
            catch { /* sessiz geç */ }

            if (hizmetBedeliToplam <= 0)
                hizmetBedeliToplam = _config.Value.BonusMiktari; // admin panelinde değiştirilebilir

            // 4) Yardımcılar ve kalem listesi
            decimal AdminKalemToplam(AlisverisSitesiFinal.Models.SiparisKalemi k)
                => (k.AdminFiyatAnlik ?? (k.BirimFiyat * k.Miktar));

            decimal UrunKalemToplam(AlisverisSitesiFinal.Models.SiparisKalemi k)
                => (k.SaticiTeklifAnlik ?? (k.BirimFiyat * k.Miktar));

            var kalemler = s.SiparisKalemleri ?? new List<AlisverisSitesiFinal.Models.SiparisKalemi>();

            // 5) Toplamları hesapla
            decimal adminToplam = kalemler.Sum(k => AdminKalemToplam(k));
            decimal urunToplam = kalemler.Sum(k => UrunKalemToplam(k));

            // 6) Hizmet bedelini kalemlere oransal dağıt + kalem bazlı kâr
            foreach (var k in kalemler)
            {
                decimal pay = adminToplam > 0 ? (AdminKalemToplam(k) / adminToplam) : 0m;

                k.HizmetBedeli = Math.Round(hizmetBedeliToplam * pay, 2, MidpointRounding.AwayFromZero);

                var kalemKari = AdminKalemToplam(k) - (UrunKalemToplam(k) + k.HizmetBedeli);
                k.SirketKari = Math.Round(kalemKari, 2, MidpointRounding.AwayFromZero);
            }

            // 7) Sipariş toplamlarını yaz
            s.ToplamHizmetBedeli = kalemler.Sum(x => x.HizmetBedeli);
            s.ToplamSirketKari = kalemler.Sum(x => x.SirketKari);

            _context.Siparisler.Update(s);
            await _context.SaveChangesAsync();  // <-- PDF burada artık doğru değerleri görecek

            // 8) (Opsiyonel) otomatik e-fatura denemesi
            var faturaOk = await _invoiceService.CreateForOrderAsync(s.Id);
            if (!faturaOk)
                TempData["ErrorMessage"] = "Fatura otomatik oluşturulamadı. Lütfen 'Fatura Oluştur' ile tekrar deneyin.";

            // ---------------- MAIL GÖNDER ----------------
            if (!string.IsNullOrEmpty(s.IdentityUserId))
            {
                var user = await _userManager.FindByIdAsync(s.IdentityUserId);
                if (!string.IsNullOrEmpty(user?.Email))
                {
                    string subject = $"Siparişiniz #{s.Id} Onaylandı";
                    string body = $@"
                Merhaba {user.UserName},<br/>
                Siparişiniz onaylanmıştır.<br/>
                Sipariş detayları:<br/>
                - Sipariş ID: {s.Id}<br/>
                - Durum: {s.Durum}<br/><br/>
                Teşekkürler,<br/>
                Maraline";
                    await _emailSender.SendEmailAsync(user.Email, subject, body);
                }
            }



            return Json(new { ok = true, id, status = s.Durum, next = "Kargola", message = $"Sipariş #{id} onaylandı." });
        }

        [HttpPost]
        public async Task<IActionResult> Reddet(int siparisId, string redSebebi)
        {
            var siparis = await _context.Siparisler.FindAsync(siparisId);
            if (siparis == null) return NotFound();

            siparis.Durum = "Reddedildi";
            siparis.ReddetmeNedeni = redSebebi;
            _context.Siparisler.Update(siparis);
            await _context.SaveChangesAsync();

            // MAIL GÖNDER
            if (!string.IsNullOrEmpty(siparis.IdentityUserId))
            {
                var user = await _userManager.FindByIdAsync(siparis.IdentityUserId);
                if (!string.IsNullOrEmpty(user?.Email))
                {
                    string subject = $"Siparişiniz #{siparis.Id} Reddedildi";
                    string body = $@"
                        Merhaba {user.UserName},<br/>
                        Siparişiniz maalesef reddedildi.<br/>
                        Reddetme Nedeni: {redSebebi}<br/><br/>
                        Teşekkürler,<br/>
                        Maraline";

                    await _emailSender.SendEmailAsync(user.Email, subject, body);
                }
            }

            TempData["SuccessMessage"] = "Sipariş reddedildi ve kullanıcı bilgilendirildi.";
            return RedirectToAction("Index");
        }

        [HttpPost, Authorize(Roles = "Satici,Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Kargola(int id)
        {
            var me = await _userManager.GetUserAsync(User);
            var s = await _context.Siparisler
                .Include(x => x.SiparisKalemleri).ThenInclude(k => k.Urun)
                .Include(x => x.IdentityUserId)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            if (!User.IsInRole("Admin") && !await KullaniciBuSiparisiYonetebilirMi(me, s)) return Forbid();

            s.Durum = "Kargolandı";
            await _context.SaveChangesAsync();
            // 8) (Opsiyonel) otomatik e-fatura denemesi
            var faturaOk = await _invoiceService.CreateForOrderAsync(s.Id);
            if (!faturaOk)
                TempData["ErrorMessage"] = "Fatura otomatik oluşturulamadı. Lütfen 'Fatura Oluştur' ile tekrar deneyin.";
            // ---------------- MAIL GÖNDER ----------------
            if (!string.IsNullOrEmpty(s.IdentityUserId))
            {
                var user = await _userManager.FindByIdAsync(s.IdentityUserId);
                if (user != null)
                {
                    string subject = $"Siparişiniz #{s.Id} Kargolandı";
                    string body = $@"
                Merhaba {user.UserName},<br/>
                Siparişiniz kargoya verilmiştir.<br/>
                Sipariş detayları:<br/>
                - Sipariş ID: {s.Id}<br/>
                - Durum: {s.Durum}<br/><br/>
                Teşekkürler,<br/>
                Maraline";
                    if (!string.IsNullOrEmpty(user.Email))
                    {
                        await _emailSender.SendEmailAsync(user.Email, subject, body);
                    }
                }
            }
            return Json(new { ok = true, id, status = s.Durum, next = "Teslim Edildi", message = $"Sipariş #{id} kargolandı." });
        }

        [HttpPost, Authorize(Roles = "Satici,Admin"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Tamamla(int id)
        {
            var me = await _userManager.GetUserAsync(User);
            var s = await _context.Siparisler
                .Include(x => x.SiparisKalemleri).ThenInclude(k => k.Urun)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            if (!User.IsInRole("Admin") && !await KullaniciBuSiparisiYonetebilirMi(me, s)) return Forbid();

            s.Durum = "Tamamlandı";
            await _context.SaveChangesAsync();
            // 8) (Opsiyonel) otomatik e-fatura denemesi
            var faturaOk = await _invoiceService.CreateForOrderAsync(s.Id);
            if (!faturaOk)
                TempData["ErrorMessage"] = "Fatura otomatik oluşturulamadı. Lütfen 'Fatura Oluştur' ile tekrar deneyin.";

            // ---------------- MAIL GÖNDER ----------------
            if (!string.IsNullOrEmpty(s.IdentityUserId))
            {
                var user = await _userManager.FindByIdAsync(s.IdentityUserId);
                if (user != null)
                {
                    string subject = $"Siparişiniz #{s.Id} Teslim Edildi";
                    string body = $@"
                Merhaba {user.UserName},<br/>
                Siparişiniz teslim edilmiştir.<br/>
                Sipariş detayları:<br/>
                - Sipariş ID: {s.Id}<br/>
                - Durum: {s.Durum}<br/><br/>
                Teşekkürler,<br/>
                Maraline";
                    if (!string.IsNullOrEmpty(user.Email))
                    {
                        await _emailSender.SendEmailAsync(user.Email, subject, body);
                    }
                }
            }

            return Json(new { ok = true, id, status = s.Durum, next = (string?)null, message = $"Sipariş #{id} teslim edildi." });
           
            

        }

        private async Task<bool> KullaniciBuSiparisiYonetebilirMi(Kullanici? me, Siparis? siparis)
        {
            if (me == null || siparis == null) return false;
            var myStore = await _context.Magazalar.AsNoTracking().FirstOrDefaultAsync(x => x.OwnerUserId == me.Id);
            if (myStore == null) return false;

            return siparis.SiparisKalemleri != null &&
                   siparis.SiparisKalemleri.Any(k => k.Urun != null && k.Urun.StoreId == myStore.Id);
        }

        // =======================
        // ===== İADE =====
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IadeTalepEt(int id, string? aciklama)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var siparis = await _context.Siparisler
                .Include(s => s.SiparisKalemleri).ThenInclude(k => k.Urun)
                .FirstOrDefaultAsync(s => s.Id == id && s.IdentityUserId == me.Id);

            if (siparis == null)
            {
                TempData["ErrorMessage"] = "Sipariş bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (string.Equals(siparis.Durum, "Beklemede", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Hazırlanma aşamasındaki sipariş için iade talebi açılamaz.";
                return RedirectToAction(nameof(Index));
            }

            var iadeSonTarih = siparis.SiparisTarihi.AddDays(IADE_SURESI_GUN);
            if (DateTime.Now > iadeSonTarih)
            {
                TempData["ErrorMessage"] = $"İade süreniz dolmuştur. Son tarih: {iadeSonTarih:dd.MM.yyyy}.";
                return RedirectToAction(nameof(Index));
            }

            bool already = await _context.IadeTalepleri
                .AnyAsync(t => t.SiparisId == siparis.Id && t.Durum == "Beklemede");
            if (already)
            {
                TempData["ErrorMessage"] = "Bu sipariş için bekleyen iade talebiniz var.";
                return RedirectToAction(nameof(Index));
            }

            var talep = new IadeTalebi
            {
                SiparisId = siparis.Id,
                KullaniciId = me.Id,
                Durum = "Beklemede",
                Aciklama = aciklama
            };

            _context.IadeTalepleri.Add(talep);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"#{siparis.Id} numaralı sipariş için iade talebiniz oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet, Authorize(Roles = "Satici,Admin")]
        public async Task<IActionResult> IadeTalepleri()
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                IQueryable<IadeTalebi> q = _context.IadeTalepleri
                    .AsNoTracking()
                    .Include(t => t.Siparis).ThenInclude(s => s!.SiparisKalemleri).ThenInclude(k => k.Urun);

                if (!User.IsInRole("Admin"))
                {
                    var myStore = await _context.Magazalar.AsNoTracking().FirstOrDefaultAsync(x => x.OwnerUserId == me.Id);
                    if (myStore == null) { ViewBag.Ibanlar = new Dictionary<string, string>(); return View(new List<IadeTalebi>()); }

                    q = q.Where(t =>
                        t.Siparis != null &&
                        t.Siparis.SiparisKalemleri != null &&
                        t.Siparis.SiparisKalemleri.Any(k => k.Urun != null && k.Urun.StoreId == myStore.Id));
                }

                var list = await q.OrderByDescending(t => t.TalepTarihi).ToListAsync();

                // Admin için IBAN haritası; değilse de boş sözlük ver → view güvenli
                if (User.IsInRole("Admin"))
                {
                    var userIds = list.Select(t => t.KullaniciId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
                    var ibanMap = await _context.Users
                        .AsNoTracking()
                        .Where(u => userIds.Contains(u.Id))
                        .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.IBAN) ? "IBAN tanımlı değil" : u.IBAN);
                    ViewBag.Ibanlar = ibanMap;
                }
                else
                {
                    ViewBag.Ibanlar = new Dictionary<string, string>();
                }

                return View(list);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"İade talepleri yüklenemedi: {ex.Message}";
                ViewBag.Ibanlar = new Dictionary<string, string>();
                return View(new List<IadeTalebi>());
            }
        }

        [HttpPost, Authorize(Roles = "Satici,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IadeOnayla(int talepId)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var talep = await _context.IadeTalepleri
                .Include(t => t.Siparis).ThenInclude(s => s!.SiparisKalemleri).ThenInclude(k => k.Urun)
                .FirstOrDefaultAsync(t => t.Id == talepId);

            if (talep == null || talep.Siparis == null) return NotFound();
            if (!User.IsInRole("Admin") && !await KullaniciBuSiparisiYonetebilirMi(me, talep.Siparis)) return Forbid();
            if (!string.Equals(talep.Durum, "Beklemede", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Talep zaten sonuçlandırılmış.";
                return RedirectToAction(nameof(IadeTalepleri));
            }

            talep.Siparis.Durum = "İade Onaylandı";

            if (string.IsNullOrEmpty(talep.RmaKodu))
            {
                talep.RmaKodu = $"RMA-{DateTime.UtcNow:yyyyMMddHHmmss}-{talep.SiparisId}";
                talep.RmaOlusturmaTarihi = DateTime.Now;

                var myStore = await _context.Magazalar.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.OwnerUserId == me.Id);

                string fallbackAdres = "Maraline Depo – İade Birimi, İstanbul, 34000, +90 555 000 00 00";
                talep.IadeAdres = !string.IsNullOrWhiteSpace(myStore?.Adres) ? myStore!.Adres : fallbackAdres;
                talep.IadeTalimat = "Lütfen RMA kodunu paketin üzerine yazın ve 7 gün içinde kargoya verin.";
            }

            talep.Durum = "Onaylandı";
            talep.SonucTarihi = DateTime.Now;

            _context.IadeTalepleri.Update(talep);
            _context.Siparisler.Update(talep.Siparis);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"#{talep.SiparisId} iade onaylandı. RMA ve iade adresi oluşturuldu.";
            return RedirectToAction(nameof(IadeTalepleri));
        }

        [HttpPost, Authorize(Roles = "Satici,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IadeReddet(int talepId, string? neden)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var talep = await _context.IadeTalepleri
                .Include(t => t.Siparis).ThenInclude(s => s!.SiparisKalemleri).ThenInclude(k => k.Urun)
                .FirstOrDefaultAsync(t => t.Id == talepId);

            if (talep == null || talep.Siparis == null)
            {
                TempData["ErrorMessage"] = "İade talebi bulunamadı.";
                return RedirectToAction(nameof(IadeTalepleri));
            }

            if (!User.IsInRole("Admin") && !await KullaniciBuSiparisiYonetebilirMi(me, talep.Siparis))
                return Forbid();

            if (!string.Equals(talep.Durum, "Beklemede", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Sadece 'Beklemede' durumundaki talepler reddedilebilir.";
                return RedirectToAction(nameof(IadeTalepleri));
            }

            talep.Durum = "Reddedildi";
            talep.ReddetmeNedeni = string.IsNullOrWhiteSpace(neden) ? "Sebep belirtilmedi" : neden.Trim();
            talep.SonucTarihi = DateTime.Now;

            _context.IadeTalepleri.Update(talep);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"#{talep.SiparisId} iade talebi reddedildi.";
            return RedirectToAction(nameof(IadeTalepleri));
        }

        [HttpPost, Authorize(Roles = "Satici,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IadeUlasti(int talepId)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var talep = await _context.IadeTalepleri
                .Include(t => t.Siparis).ThenInclude(s => s!.SiparisKalemleri).ThenInclude(k => k.Urun)
                .FirstOrDefaultAsync(t => t.Id == talepId);

            if (talep == null || talep.Siparis == null) return NotFound();
            if (!User.IsInRole("Admin") && !await KullaniciBuSiparisiYonetebilirMi(me, talep.Siparis)) return Forbid();

            if (!string.Equals(talep.Durum, "Onaylandı", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "İlerlemek için talep 'Onaylandı' durumda olmalıdır.";
                return RedirectToAction(nameof(IadeTalepleri));
            }

            foreach (var kalem in talep.Siparis.SiparisKalemleri ?? Enumerable.Empty<SiparisKalemi>())
            {
                var urun = await _context.Uruns.FirstOrDefaultAsync(u => u.Id == kalem.UrunId);
                if (urun != null)
                {
                    urun.StokAdedi += kalem.Miktar;
                    _context.Uruns.Update(urun);
                }
            }

            var bonusHandler = new ReferralBonusHandler(_context, _userManager, _config);
            await bonusHandler.RevertOrderEffectsAsync(talep.Siparis);  // bonus/log rollback

            talep.Siparis.Durum = "İade Edildi";
            _context.Siparisler.Update(talep.Siparis);

            talep.Durum = "Tamamlandı";
            talep.SonucTarihi = DateTime.Now;
            _context.IadeTalepleri.Update(talep);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"#{talep.SiparisId} iade mağazaya ulaştı. Stoklar ve bonuslar güncellendi.";
            return RedirectToAction(nameof(IadeTalepleri));
        }

        // MAĞAZAYA İADE ULAŞTI → REDDET (yanlış/eksik/uygunsuz iade)
        [HttpPost, Authorize(Roles = "Satici,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IadeUlastiReddet(int talepId, string neden)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var talep = await _context.IadeTalepleri
                .Include(t => t.Siparis).ThenInclude(s => s!.SiparisKalemleri).ThenInclude(k => k.Urun)
                .FirstOrDefaultAsync(t => t.Id == talepId);

            if (talep == null || talep.Siparis == null) return NotFound();
            if (!User.IsInRole("Admin") && !await KullaniciBuSiparisiYonetebilirMi(me, talep.Siparis)) return Forbid();

            if (!string.Equals(talep.Durum, "Onaylandı", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "İlerlemek için talep 'Onaylandı' durumda olmalıdır.";
                return RedirectToAction(nameof(IadeTalepleri));
            }

            if (string.IsNullOrWhiteSpace(neden))
            {
                TempData["ErrorMessage"] = "Reddetme nedeni zorunludur.";
                return RedirectToAction(nameof(IadeTalepleri));
            }

            // REDDET: stok/bonus GERİ ALIMI YOK
            talep.Durum = "Reddedildi";
            talep.ReddetmeNedeni = neden.Trim();
            talep.SonucTarihi = DateTime.Now;
            _context.IadeTalepleri.Update(talep);

            // siparişi iade sürecinden çıkar (mantıklı geri adım)
            talep.Siparis.Durum = "Kargolandı";
            _context.Siparisler.Update(talep.Siparis);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"#{talep.SiparisId} iade reddedildi.";
            return RedirectToAction(nameof(IadeTalepleri));
        }
        [HttpPost, Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IadeOdemeYap(int talepId, decimal tutar, string yontem = "Kart İadesi")
        {
            var talep = await _context.IadeTalepleri
                .Include(t => t.Siparis)
                .FirstOrDefaultAsync(t => t.Id == talepId);

            if (talep == null || talep.Siparis == null)
            {
                TempData["ErrorMessage"] = "İade talebi bulunamadı.";
                return RedirectToAction(nameof(IadeTalepleri));
            }

            // İade lojistiği tamamlanmış olmalı
            if (!string.Equals(talep.Durum, "Tamamlandı", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(talep.Siparis.Durum, "İade Edildi", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Önce iade ürünü mağazaya ulaşmalı ve 'İade Edildi' durumuna alınmalı.";
                return RedirectToAction(nameof(IadeTalepleri));
            }

            // Tutar kontrolü
            if (tutar <= 0 || tutar > talep.Siparis.ToplamTutar)
            {
                TempData["ErrorMessage"] = "Geçersiz iade tutarı.";
                return RedirectToAction(nameof(IadeTalepleri));
            }

            // Ödeme kaydı
            talep.IadeTutar = Math.Round(tutar, 2);
            talep.IadeYontemi = string.IsNullOrWhiteSpace(yontem) ? "Kart İadesi" : yontem.Trim();
            talep.IadeYapanUserId = (await _userManager.GetUserAsync(User))?.Id;
            talep.IadeOdemeTarihi = DateTime.Now;

            _context.IadeTalepleri.Update(talep);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"#{talep.SiparisId} için {tutar:n2} TL iade ödemesi kaydedildi ({talep.IadeYontemi}).";
            return RedirectToAction(nameof(IadeTalepleri));
        }

        // =======================
        // PROFORMA PDF
        // =======================
        [HttpGet]
        public async Task<IActionResult> ProformaPdf(int id)
        {
            var s = await _context.Siparisler
                .Include(x => x.SiparisKalemleri).ThenInclude(k => k.Urun)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();

            // yetki
            if (!(User.IsInRole("Admin") || User.IsInRole("Satici")))
            {
                var me = await _userManager.GetUserAsync(User);
                if (s.IdentityUserId != me?.Id) return Forbid();
            }

            // ----- MÜŞTERİ BİLGİLERİ -----
            var user = await _userManager.FindByIdAsync(s.IdentityUserId);
            string? adSoyad =
                !string.IsNullOrWhiteSpace(user?.AdSoyad) ? user!.AdSoyad :
                (!string.IsNullOrWhiteSpace(user?.Ad) || !string.IsNullOrWhiteSpace(user?.Soyad))
                    ? $"{user?.Ad} {user?.Soyad}".Trim() : user?.UserName;
            string? email = user?.Email;
            string? telefon = user?.PhoneNumber;

            // Adres (varsa oku; yoksa "-")
            string? adresMetni = null;
            adresMetni ??= s.GetType().GetProperty("AdresMetni")?.GetValue(s)?.ToString();
            adresMetni ??= s.GetType().GetProperty("AdresText")?.GetValue(s)?.ToString();
            var adrProp = s.GetType().GetProperty("Adres")?.GetValue(s);
            if (adrProp != null)
            {
                var acik = adrProp.GetType().GetProperty("AcikAdres")?.GetValue(adrProp)?.ToString();
                var ilce = adrProp.GetType().GetProperty("Ilce")?.GetValue(adrProp)?.ToString();
                var il = adrProp.GetType().GetProperty("Il")?.GetValue(adrProp)?.ToString();
                if (!string.IsNullOrWhiteSpace(acik) || !string.IsNullOrWhiteSpace(ilce) || !string.IsNullOrWhiteSpace(il))
                    adresMetni = $"{acik} {(ilce ?? "")} {(il ?? "")}".Trim();
            }

            // ----- TOPLAMLAR 0'sa PDF'ten önce HESAPLA -----
            var kalemler = s.SiparisKalemleri ?? new List<AlisverisSitesiFinal.Models.SiparisKalemi>();

            decimal AdminKalemToplam(AlisverisSitesiFinal.Models.SiparisKalemi k)
                => (k.AdminFiyatAnlik ?? (k.BirimFiyat * k.Miktar));

            decimal UrunKalemToplam(AlisverisSitesiFinal.Models.SiparisKalemi k)
                => (k.SaticiTeklifAnlik ?? (k.BirimFiyat * k.Miktar));

            if ((s.ToplamHizmetBedeli == 0 && s.ToplamSirketKari == 0) && kalemler.Any())
            {
                decimal adminToplam = kalemler.Sum(k => AdminKalemToplam(k));
                decimal urunToplam = kalemler.Sum(k => UrunKalemToplam(k));

                decimal hizmetTop = 0m;
                try
                {
                    if (!string.IsNullOrWhiteSpace(s.OrderRefKey))
                        hizmetTop = await new ReferralBonusHandler(_context, _userManager, _config)
                                              .GetOrderDistributedBonusTotalAsync(s.OrderRefKey);
                }
                catch { }
                if (hizmetTop <= 0)
                    hizmetTop = _config.Value.BonusMiktari;

                s.ToplamHizmetBedeli = Math.Round(hizmetTop, 2);
                s.ToplamSirketKari = Math.Round(adminToplam - (urunToplam + s.ToplamHizmetBedeli), 2);
            }

            // ----- PDF ÜRET -----
            var pdf = _invoicePdf.Generate(
                s,
                musteriAdSoyad: adSoyad,
                musteriEmail: email,
                musteriTelefon: telefon,
                musteriAdres: adresMetni
            );

            var fileName = $"Maraline_Proforma_{s.Id}_{DateTime.Now:yyyyMMddHHmm}.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        // =======================
        // F A T U R A
        // =======================
        [Authorize(Roles = "Satici,Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> FaturaOlustur(int id)
        {
            var ok = await _invoiceService.CreateForOrderAsync(id);
            if (!ok) TempData["ErrorMessage"] = "Fatura oluşturulamadı.";
            else TempData["SuccessMessage"] = "Fatura oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> FaturaGor(int id)
        {
            var s = await _context.Siparisler.FindAsync(id);
            if (s == null) return NotFound();

            if (string.Equals(s.FaturaDurumu, "Kesildi", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(s.EFaturaNo))
            {
                return RedirectToAction(nameof(FaturaDetay), new { id = s.Id });
            }

            TempData["ErrorMessage"] = "Fatura henüz hazır değil.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> FaturaDetay(int id)
        {
            var s = await _context.Siparisler.FindAsync(id);
            if (s == null) return NotFound();

            ViewBag.FaturaNo = s.EFaturaNo;
            ViewBag.Durum = s.FaturaDurumu;
            ViewBag.Tarih = s.EFaturaTarihi;
            return View(); // Views/Siparis/FaturaDetay.cshtml
        }
    }
}
