using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options; // <-- eklendi

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize]
    public class ParaCekmeController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;
        private readonly IOptions<ReferralConfig> _refConfig; // <-- eklendi

        // İş kuralı sabitleri
        private const int IADE_SURESI_GUN = 14; // 14 günlük bekleme

        public ParaCekmeController(
            UygulamaDbContext context,
            UserManager<Kullanici> userManager,
            IOptions<ReferralConfig> refConfig // <-- eklendi
        )
        {
            _context = context;
            _userManager = userManager;
            _refConfig = refConfig; // <-- eklendi
        }

        // --------------------- Yardımcılar ---------------------

        // 14 gün kilidi: son 14 günde, iade edilmemiş bir sipariş var mı?
        // (Beklemede/Onaylandı/Kargolandı/Tamamlandı vs. → kilit; "İade Edildi/Iade Edildi" → kilit değil)
        private async Task<bool> HasFourteenDayLockAsync(string userId)
        {
            var limit = DateTime.Now.AddDays(-IADE_SURESI_GUN);

            return await _context.Siparisler
                .Where(s => s.IdentityUserId == userId && s.SiparisTarihi >= limit)
                .AnyAsync(s => s.Durum != "İade Edildi" && s.Durum != "Iade Edildi");
        }

        // Eşik için NET harcama:
        // - İade edilmiş ("İade Edildi", "Iade Edildi")
        // - Reddedilmiş ("Reddedildi", "Red Edildi")
        // - İptal edilmiş ("İptal", "Iptal", "İptal Edildi", "Iptal Edildi")
        // siparişleri HARİÇ bırak.
        // + Üzerinde AKTİF iade talebi (Beklemede/Onaylandı/Onaylandi/Ulaştı/Ulasti) bulunan siparişleri de HARİÇ bırak.
        private async Task<decimal> GetNetHarcamaForThresholdAsync(string userId)
        {
            // İade talebi devam eden durumlar (çeşitli yazım varyasyonları)
            var aktifIadeDurumlari = new[] { "Beklemede", "Onaylandı", "Onaylandi", "Ulaştı", "Ulasti" };

            // Eşiğe dahil edilmeyecek sipariş durumları
            var dislaSiparisDurumlari = new[]
            {
                "İade Edildi","Iade Edildi",
                "Reddedildi","Red Edildi",
                "İptal","Iptal","İptal Edildi","Iptal Edildi"
            };

            // Aktif iade talebi olan sipariş id'lerini çek
            var aktifIadeSiparisIdleri = await _context.IadeTalepleri
                .Where(t => t.Siparis != null && t.Siparis.IdentityUserId == userId)
                .Where(t => aktifIadeDurumlari.Contains(t.Durum))
                .Select(t => t.SiparisId)
                .Distinct()
                .ToListAsync();

            // Eşiğe dahil harcama
            var net = await _context.Siparisler
                .Where(s => s.IdentityUserId == userId)
                .Where(s => !dislaSiparisDurumlari.Contains(s.Durum))   // iade/iptal/reddedileni hariç tut
                .Where(s => !aktifIadeSiparisIdleri.Contains(s.Id))     // aktif iade talebi olanları hariç tut
                .SumAsync(s => (decimal?)s.ToplamTutar) ?? 0m;

            return net < 0 ? 0 : net;
        }

        // --------------------- Kullanıcının Talepleri ---------------------
        public async Task<IActionResult> KullanicininTalepleri()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var talepler = await _context.ParaCekmeTalepleri
                .Where(t => t.KullaniciId == user.Id)
                .OrderByDescending(t => t.TalepTarihi)
                .ToListAsync();

            return View(talepler);
        }

        // --- IBAN'dan banka adı & logo (TR IBAN 5 hane banka kodu) ---
        private (string Ad, string LogoUrl) GetBankaBilgisiFromIban(string? iban)
        {
            if (string.IsNullOrWhiteSpace(iban))
                return ("Bilinmeyen Banka", "/images/banks/generic-bank.svg");

            iban = iban.Replace(" ", "").ToUpperInvariant();
            if (!(iban.StartsWith("TR") && iban.Length >= 9))
                return ("Bilinmeyen Banka", "/images/banks/generic-bank.svg");

            var bankaKodu = iban.Substring(4, 5);
            var map = new Dictionary<string, (string Ad, string Logo)>
            {
                { "00010", ("Ziraat Bankası", "/images/banks/ziraat.jpg") },
                { "00062", ("VakıfBank", "/images/banks/vakifbank.png") },
                { "00015", ("Halkbank", "/images/banks/halkbank.png") },
                { "00134", ("İş Bankası", "/images/banks/isbank.jpeg") },
                { "00146", ("Garanti BBVA", "/images/banks/garanti.png") },
                { "00067", ("Yapı Kredi", "/images/banks/yapikredi.png") },
                { "00032", ("TEB Bankası", "/images/banks/TEB.jpeg") },
            };
            if (map.TryGetValue(bankaKodu, out var info)) return info;
            return ($"Banka Kodu: {bankaKodu}", "/images/banks/generic-bank.svg");
        }

        // --------------------- CREATE (GET) ---------------------
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(user.IBAN))
            {
                TempData["Error"] = "Para çekme talebi oluşturabilmek için önce IBAN bilgisi eklemelisiniz.";
                return RedirectToAction("IBANGuncelle");
            }

            ViewData["AdSoyad"] = $"{user.Ad} {user.Soyad}";
            ViewData["IBAN"] = user.IBAN;

            var (bankaAd, logo) = GetBankaBilgisiFromIban(user.IBAN);
            ViewData["BankaAd"] = bankaAd;
            ViewData["BankaLogo"] = logo;

            // Kullanıcı görünümü için flags/bilgiler
            ViewData["Balance"] = user.ToplamKazanc; // mevcut kazanç
            ViewData["Has14DayLock"] = await HasFourteenDayLockAsync(user.Id);

            var netForThreshold = await GetNetHarcamaForThresholdAsync(user.Id);
            var cekimEsik = (decimal)(_refConfig.Value?.AktiflikHarcamaLimiti ?? 4000m); // <-- konfigden
            ViewData["NetForThreshold"] = netForThreshold;
            ViewData["Eşik"] = cekimEsik;

            // canWithdraw: sadece bakiyeye bağlı (diğer kontroller view+post'ta var)
            ViewData["CanWithdraw"] = user.ToplamKazanc > 0;

            return View(new ParaCekmeTalebi());
        }

        // --------------------- CREATE (POST) ---------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Tutar")] ParaCekmeTalebi form)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // View için tekrar doldur
            ViewData["AdSoyad"] = $"{user.Ad} {user.Soyad}";
            ViewData["IBAN"] = user.IBAN ?? string.Empty;
            var (bankaAd, logo) = GetBankaBilgisiFromIban(user.IBAN);
            ViewData["BankaAd"] = bankaAd;
            ViewData["BankaLogo"] = logo;

            // IBAN zorunlu
            if (string.IsNullOrWhiteSpace(user.IBAN))
            {
                TempData["Error"] = "Para çekme talebi için önce IBAN bilgisi ekleyin.";
                return RedirectToAction("IBANGuncelle");
            }

            // 1) 14 günlük kesinleşme kilidi
            if (await HasFourteenDayLockAsync(user.Id))
            {
                ModelState.AddModelError("", "14 günlük kesinleşme süreci bitmeden para çekme talebinde bulunamazsınız.");
            }

            // 2) Eşik (aktif iade siparişleri hesaba dahil edilmez) — konfigden
            var netForThreshold = await GetNetHarcamaForThresholdAsync(user.Id);
            var cekimEsik = (decimal)(_refConfig.Value?.AktiflikHarcamaLimiti ?? 4000m);
            if (netForThreshold < cekimEsik)
            {
                ModelState.AddModelError("", $"Çekim için {cekimEsik:n0} ₺ net harcama şartını sağlamanız gerekiyor. " +
                                             "(Aktif iade sürecindeki siparişler bu hesaba dahil edilmez.)");
            }

            // 3) Kullanıcının çekilebilir bakiyesi: ToplamKazanc
            if (user.ToplamKazanc <= 0)
            {
                ModelState.AddModelError("", "Kazancınız bulunmuyor. Para çekme talebi oluşturamazsınız.");
            }

            // Model’de Required olan ama formdan gelmeyen alanları temizle
            ModelState.Remove(nameof(ParaCekmeTalebi.KullaniciId));
            ModelState.Remove(nameof(ParaCekmeTalebi.IBAN));
            ModelState.Remove(nameof(ParaCekmeTalebi.IBANSahibiAdSoyad));

            if (!ModelState.IsValid)
            {
                ViewData["Balance"] = user.ToplamKazanc;
                ViewData["Has14DayLock"] = await HasFourteenDayLockAsync(user.Id);
                ViewData["NetForThreshold"] = netForThreshold;
                ViewData["Eşik"] = cekimEsik;
                ViewData["CanWithdraw"] = user.ToplamKazanc > 0;
                return View(form);
            }

            if (form.Tutar <= 0 || form.Tutar > user.ToplamKazanc)
            {
                ModelState.AddModelError("", $"Tutar 0'dan büyük ve mevcut kazanç ({user.ToplamKazanc:n2} TL) kadar olmalıdır.");
                ViewData["Balance"] = user.ToplamKazanc;
                ViewData["Has14DayLock"] = await HasFourteenDayLockAsync(user.Id);
                ViewData["NetForThreshold"] = netForThreshold;
                ViewData["Eşik"] = cekimEsik;
                ViewData["CanWithdraw"] = user.ToplamKazanc > 0;
                return View(form);
            }

            // Sunucu tarafında doldur
            form.KullaniciId = user.Id;
            form.IBANSahibiAdSoyad = $"{user.Ad} {user.Soyad}";
            form.IBAN = user.IBAN!;
            form.TalepTarihi = DateTime.Now;
            form.OnaylandiMi = false;
            form.ReddedildiMi = false;
            form.AdminNotu = null;

            _context.ParaCekmeTalepleri.Add(form);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Para çekme talebiniz oluşturuldu. Admin onayı sonrası bakiyeniz düşülecektir.";
            return RedirectToAction("KullanicininTalepleri");
        }

        // --------------------- IBAN Güncelle (GET) ---------------------
        [HttpGet]
        public async Task<IActionResult> IBANGuncelle()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var vm = new IBANGuncelleViewModel
            {
                AdSoyad = $"{user.Ad} {user.Soyad}",
                IBAN = (user.IBAN ?? string.Empty).Replace(" ", "")
            };

            var (bankaAd, logo) = GetBankaBilgisiFromIban(vm.IBAN);
            ViewData["BankaAd"] = bankaAd;
            ViewData["BankaLogo"] = logo;

            return View(vm);
        }

        // --------------------- IBAN Güncelle (POST) ---------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IBANGuncelle(IBANGuncelleViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            vm.AdSoyad = $"{user.Ad} {user.Soyad}";
            var clean = IBANGuncelleViewModel.CleanIban(vm.IBAN);

            if (clean.Length != 26 || !System.Text.RegularExpressions.Regex.IsMatch(clean, @"^TR\d{24}$"))
            {
                ModelState.AddModelError(nameof(vm.IBAN), "IBAN formatı hatalı görünüyor.");
                var (bankaAd, logo) = GetBankaBilgisiFromIban(clean);
                ViewData["BankaAd"] = bankaAd;
                ViewData["BankaLogo"] = logo;
                return View(vm);
            }

            user.IBAN = clean;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "IBAN bilginiz güncellendi.";
            return RedirectToAction("Create");
        }

        // --------------------- Admin Talepler Listesi ---------------------
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminTalepler()
        {
            var talepler = await _context.ParaCekmeTalepleri
                .OrderByDescending(t => t.TalepTarihi)
                .ToListAsync();

            foreach (var t in talepler)
            {
                var kullanici = await _userManager.FindByIdAsync(t.KullaniciId ?? string.Empty);
                t.KullaniciAdSoyad = kullanici != null ? $"{kullanici.Ad} {kullanici.Soyad}" : "Bilinmiyor";
            }

            return View(talepler);
        }

        // --------------------- Admin Onayla ---------------------
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Onayla(int id)
        {
            var talep = await _context.ParaCekmeTalepleri.FirstOrDefaultAsync(t => t.Id == id);
            if (talep == null || talep.OnaylandiMi || talep.ReddedildiMi)
                return NotFound();

            var kullanici = await _userManager.FindByIdAsync(talep.KullaniciId ?? string.Empty);
            if (kullanici == null) return NotFound();

            // 👇 Atomik işlem
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.Entry(kullanici).ReloadAsync(); // en güncel bakiye
                if (kullanici.ToplamKazanc < talep.Tutar)
                {
                    TempData["Hata"] = "Yetersiz kazanç nedeniyle onaylanamadı.";
                    return RedirectToAction(nameof(AdminTalepler));
                }

                talep.OnaylandiMi = true;
                talep.OnayTarihi = DateTime.UtcNow;
                kullanici.ToplamKazanc -= talep.Tutar;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = "Talep onaylandı ve kazançtan düşüldü.";
                return RedirectToAction(nameof(AdminTalepler));
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Onay sırasında bir hata oluştu. İşlem iptal edildi.";
                return RedirectToAction(nameof(AdminTalepler));
            }
        }


        // --------------------- Admin Reddet ---------------------
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reddet(int id)
        {
            var talep = await _context.ParaCekmeTalepleri.FirstOrDefaultAsync(t => t.Id == id);
            if (talep == null || talep.OnaylandiMi || talep.ReddedildiMi)
                return NotFound();

            talep.ReddedildiMi = true;
            talep.AdminNotu = talep.AdminNotu ?? "Reddedildi";
            await _context.SaveChangesAsync();

            TempData["Info"] = "Talep reddedildi.";
            return RedirectToAction(nameof(AdminTalepler));
        }
    }
}
