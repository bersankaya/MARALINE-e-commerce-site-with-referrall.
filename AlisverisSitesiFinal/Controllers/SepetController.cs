using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AlisverisSitesiFinal.Helpers;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Policy = "CanPurchase")]
    public class SepetController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;
        private readonly IOptions<ReferralConfig> _config;

        public SepetController(UygulamaDbContext context, UserManager<Kullanici> userManager, IOptions<ReferralConfig> config)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
        }

        public class CartItemDto
        {
            public int UrunId { get; set; }
            public int Miktar { get; set; }
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            var sepetUrunleri = await _context.Sepet
                .Include(s => s.Urun)
                .Where(s => s.KullaniciId == user.Id)
                .ToListAsync();

            // 💰 Fiyat hesapları (satır toplamı ve genel toplam)
            // View tarafında kolayca erişebilmek için map olarak geçiyoruz.
            var fiyatListesi = sepetUrunleri.Select(s =>
            {
                var birim = FiyatHelper.GetGorunecekFiyat(user, s.Urun!) ?? 0m;
                var satir = birim * s.Miktar;
                return new { s.Id, Birim = birim, Satir = satir };
            }).ToList();

            ViewBag.Fiyatlar = fiyatListesi.ToDictionary(x => x.Id, x => new
            {
                Birim = x.Birim,
                Satir = x.Satir
            });

            ViewBag.AraToplam = fiyatListesi.Sum(x => x.Satir);

            return View(sepetUrunleri);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ekle(
            int urunId, int miktar = 1,
            string? renk = null, string? beden = null, string? boyut = null,
            string? numara = null, string? kapasite = null, string? materyal = null, string? desen = null)
        {
            if (User.IsInRole("Satici") || User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Admin ve Satıcı hesapları alışveriş yapamaz.";
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            var urun = await _context.Uruns.FindAsync(urunId);
            if (urun == null) return NotFound();

            var mevcut = await _context.Sepet.FirstOrDefaultAsync(s =>
                s.KullaniciId == user.Id && s.UrunId == urun.Id &&
                s.Renk == renk && s.Beden == beden && s.Boyut == boyut &&
                s.Numara == numara && s.Kapasite == kapasite && s.Materyal == materyal && s.Desen == desen
            );

            if (mevcut != null)
            {
                mevcut.Miktar += miktar;
                _context.Sepet.Update(mevcut);
            }
            else
            {
                var yeni = new SepetKalemi
                {
                    KullaniciId = user.Id,
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
                };
                _context.Sepet.Add(yeni);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{urun.Ad} sepete eklendi.";
            return RedirectToAction("Index", "Uruns");
        }

        [HttpPost]
        public async Task<IActionResult> SepettenSil(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var item = await _context.Sepet.FirstOrDefaultAsync(s => s.Id == id && s.KullaniciId == user!.Id);
            if (item != null)
            {
                _context.Sepet.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> TopluSil()
        {
            var user = await _userManager.GetUserAsync(User);
            var sepet = await _context.Sepet
                .Where(s => s.KullaniciId == user!.Id)
                .ToListAsync();

            _context.Sepet.RemoveRange(sepet);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Sepetiniz boşaltıldı.";
            return RedirectToAction("Index");
        }

        // ⬆️⬇️ Miktarı artır/azalt için pratik endpoint'ler (opsiyonel)
        [HttpPost]
        public async Task<IActionResult> Artir(int id) => await DegistirMiktarAsync(id, +1);

        [HttpPost]
        public async Task<IActionResult> Azalt(int id) => await DegistirMiktarAsync(id, -1);

        private async Task<IActionResult> DegistirMiktarAsync(int id, int delta)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            var kalem = await _context.Sepet
                .Include(s => s.Urun)
                .FirstOrDefaultAsync(s => s.Id == id && s.KullaniciId == user.Id);

            if (kalem == null) return NotFound();

            var yeniMiktar = kalem.Miktar + delta;
            if (yeniMiktar < 1) yeniMiktar = 1;

            // Üst sınır: stok
            if (kalem.Urun != null && kalem.Urun.StokAdedi > 0 && yeniMiktar > kalem.Urun.StokAdedi)
                yeniMiktar = kalem.Urun.StokAdedi;

            if (yeniMiktar != kalem.Miktar)
            {
                kalem.Miktar = yeniMiktar;
                _context.Sepet.Update(kalem);
                await _context.SaveChangesAsync();
            }

            // Yeniden Index'e dönünce fiyatlar yeniden hesaplanır (ViewBag ile)
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> GuncelleMiktar(int id, int yeniMiktar)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            var kalem = await _context.Sepet
                .Include(s => s.Urun)
                .FirstOrDefaultAsync(s => s.Id == id && s.KullaniciId == user.Id);

            if (kalem != null)
            {
                if (yeniMiktar < 1) yeniMiktar = 1;

                // stok limiti
                if (kalem.Urun != null && kalem.Urun.StokAdedi > 0 && yeniMiktar > kalem.Urun.StokAdedi)
                    yeniMiktar = kalem.Urun.StokAdedi;

                if (kalem.Miktar != yeniMiktar)
                {
                    kalem.Miktar = yeniMiktar;
                    _context.Sepet.Update(kalem);
                    await _context.SaveChangesAsync();
                }
            }

            // Dönüşte Index fiyatları tekrar hesaplar ve ekranda güncel görünür
            return RedirectToAction(nameof(Index));
        }

        // ADRES ZORUNLULUĞU: sadece doğrula → ödeme ekranına geç
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CanPurchase")]
        public async Task<IActionResult> SepetiOnayla()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            // Varsayılan adres var mı?
            var varsayilanAdres = await _context.Adresler
                .Where(a => a.KullaniciId == user.Id)
                .OrderByDescending(a => a.IsVarsayilan)
                .FirstOrDefaultAsync();

            if (varsayilanAdres == null)
            {
                TempData["ErrorMessage"] = "Sipariş verebilmek için lütfen önce bir adres ekleyin.";
                return RedirectToAction("Create", "Adresler", new { returnUrl = Url.Action("Index", "Sepet") });
            }

            // Sepette geçerli ürünler var mı? (stok/yayın/teklif/fiyat kontrolü)
            var sepetUrunleri = await _context.Sepet
                .Include(s => s.Urun)
                .Where(s => s.KullaniciId == user.Id)
                .ToListAsync();

            if (!sepetUrunleri.Any())
            { TempData["ErrorMessage"] = "Sepetinizde ürün yok."; return RedirectToAction("Index"); }

            foreach (var item in sepetUrunleri)
            {
                if (item.Urun == null || !item.Urun.YayindaMi)
                { TempData["ErrorMessage"] = $"{item.Urun?.Ad ?? "Ürün"} yayında değil."; return RedirectToAction("Index"); }

                if (item.Urun.StokAdedi < item.Miktar)
                { TempData["ErrorMessage"] = $"{item.Urun.Ad} stokta yetersiz."; return RedirectToAction("Index"); }

                // satıcı teklif/fiyat doğrulamaları mevcut akışla aynı
                var birimFiyat = FiyatHelper.GetGorunecekFiyat(user, item.Urun!) ?? 0m;
                if (birimFiyat <= 0)
                { TempData["ErrorMessage"] = $"{item.Urun.Ad} için geçerli bir satış fiyatı bulunamadı."; return RedirectToAction("Index"); }

                var teklif = item.Urun.SaticiTeklifFiyati;
                if (!teklif.HasValue || teklif.Value <= 0m)
                { TempData["ErrorMessage"] = $"{item.Urun.Ad} için satıcı teklif fiyatı tanımlı değil."; return RedirectToAction("Index"); }
            }

            // ❗Sipariş yazmıyoruz, sepeti boşaltmıyoruz.
            TempData["SuccessMessage"] = "Sepet onaylandı (Beklemede). Ödeme adımına geçiliyor…";
            return RedirectToAction("Baslat", "Odeme");
        }




        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var adresler = await _context.Adresler
                .Where(a => a.KullaniciId == user.Id)
                .OrderByDescending(a => a.IsVarsayilan)
                .ToListAsync();

            if (!adresler.Any())
            {
                // returnUrl ile adres eklemeye yönlendir
                return RedirectToAction("Create", "Adresler", new { returnUrl = Url.Action("Checkout", "Sepet") });
            }

            ViewBag.Adresler = adresler;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(int adresId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var adres = await _context.Adresler.FirstOrDefaultAsync(a => a.Id == adresId && a.KullaniciId == user.Id);
            if (adres == null)
            {
                TempData["ErrorMessage"] = "Geçersiz adres.";
                return RedirectToAction(nameof(Checkout));
            }

            // İstersen burada komple sepeti tek siparişe toplayan bir akışa geçiş yapabilirsin.
            // (Şimdilik ayrı ayrı sipariş üreten SepetiOnayla akışı varsayılan.)

            TempData["SuccessMessage"] = "Adres seçildi.";
            return RedirectToAction("Index"); // sepet sayfasına geri
        }
    }
}
