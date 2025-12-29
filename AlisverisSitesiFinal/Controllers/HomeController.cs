    // Controllers/HomeController.cs
using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Configuration;
using System.Diagnostics;

namespace AlisverisSitesiFinal.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;
        private readonly IOptions<ReferralConfig> _config;
        private readonly IConfiguration _configuration;
        public IActionResult Gizlilik() => View();
        public IActionResult MesafeliSatis() => View();
        public IActionResult IadeBilgisi() => View();
        public IActionResult Iletisim() => View();
        public HomeController(
            ILogger<HomeController> logger,
            UygulamaDbContext context,
            UserManager<Kullanici> userManager,
            IOptions<ReferralConfig> config, IConfiguration configuration)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _config = config;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index(string? kategori)
        {
            // ✅ Sadece yayında + mağazası aktif ürünler
            var q = _context.Uruns
                .Include(u => u.Kategori)
                .Where(u =>
                    u.YayindaMi &&
                    u.Durum == UrunDurum.Yayinda &&
                    _context.Magazalar.Any(m => m.Id == u.StoreId && m.AktifMi));

            if (!string.IsNullOrEmpty(kategori))
                q = q.Where(u => u.Kategori != null && u.Kategori.Ad == kategori);

            var urunler = await q.ToListAsync();

            // Bonus miktarı (mevcut yapın)
            var config = _configuration.GetSection("ReferralConfig").Get<ReferralConfig>();
            ViewBag.BonusMiktari = config?.BonusMiktari ?? 200;

            // Vitrin grupları (mevcut)
            var sliderUrunler = urunler.Where(u => u.IsSlider).ToList();
            var populerUrunler = urunler.Where(u => u.IsPopular).ToList();
            var avantajliUrunler = urunler.Where(u => u.IsAvantajli).ToList();

            // 🎯 Hedef kart sayısı
            const int hedef = 8;

            // 1) Satışa göre en çok satanlar (ONAYLI siparişler)
            var topSalesIds = await _context.SiparisKalemleri
                .Where(sk => sk.Siparis!.Durum == "Onaylandı")
                .GroupBy(sk => sk.UrunId)
                .Select(g => new { UrunId = g.Key, Toplam = g.Sum(x => x.Miktar) }) // sizde alan "Adet" ise x.Adet
                .OrderByDescending(x => x.Toplam)
                .Take(hedef * 3) // filtrelere takılabilecekleri tolere etmek için biraz geniş tut
                .Select(x => x.UrunId)
                .ToListAsync();

            // Satış sırasını korumak için index haritası
            var indexMap = topSalesIds
                .Select((id, idx) => new { id, idx })
                .ToDictionary(x => x.id, x => x.idx);

            var enCokSatilanlarSales = urunler
                .Where(u => topSalesIds.Contains(u.Id))
                .OrderBy(u => indexMap[u.Id])
                .Take(hedef)
                .ToList();

            // 2) Satış azsa, adminin işaretledikleriyle (IsCokSatan) tamamla
            int kalan = hedef - enCokSatilanlarSales.Count;
            var enCokFinal = new List<Urun>(enCokSatilanlarSales);

            if (kalan > 0)
            {
                var adminFlagged = urunler
                    .Where(u => u.IsCokSatan && !topSalesIds.Contains(u.Id)) // satış listesinde yoksa
                    .Take(kalan)
                    .ToList();

                enCokFinal.AddRange(adminFlagged);
            }

            // 3) Hâlâ eksikse, son çare popülerden doldur (boş kalmasın)
            if (enCokFinal.Count < hedef)
            {
                var kalan2 = hedef - enCokFinal.Count;
                var extra = urunler
                    .Where(u => u.IsPopular && !enCokFinal.Any(e => e.Id == u.Id))
                    .Take(kalan2)
                    .ToList();

                enCokFinal.AddRange(extra);
            }

            if (enCokFinal.Count < hedef)
            {
                var kalan2 = hedef - enCokFinal.Count;
                var extra = urunler
                    .Where(u => u.IsAvantajli && !enCokFinal.Any(e => e.Id == u.Id))
                    .Take(kalan2)
                    .ToList();

                enCokFinal.AddRange(extra);
            }

            var model = new HomeViewModel
            {
                SliderUrunler = sliderUrunler,
                PopulerUrunler = populerUrunler,
                AvantajliUrunler=avantajliUrunler,
                EnCokSatanlar = enCokFinal,   // ← artık satış-öncelikli + admin fallback + popüler fallbackx
                Kategoriler = await _context.Kategoriler.Take(5).ToListAsync()
            };

            return View(model);
        }



        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BonuslariYenidenHesapla()
        {
            var kullanicilar = await _context.Users.ToListAsync();

            foreach (var kullanici in kullanicilar)
            {
                var toplamBonus = await _context.BonusLoglari
                    .Where(b => b.KullaniciId == kullanici.Id)
                    .SumAsync(b => b.Tutar);

                kullanici.ToplamKazanilanPara = toplamBonus;
            }

            await _context.SaveChangesAsync();

            return Content($"✅ Tüm kullanıcıların ToplamKazanilanPara alanları BonusLog'a göre güncellendi.");
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // (projende var diye bırakıyorum – ürün detayı UrunsController’da da var)
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var urun = await _context.Uruns
                .Include(u => u.Kategori)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (urun == null) return NotFound();

            // ✅ Güvenlik: pasif mağaza / yayında değilse gösterme
            var storeAktif = await _context.Magazalar.AnyAsync(m => m.Id == urun.StoreId && m.AktifMi);
            if (!(urun.YayindaMi && urun.Durum == UrunDurum.Yayinda) || !storeAktif)
            {
                TempData["ErrorMessage"] = "Ürün görüntülenemiyor (mağaza pasif veya ürün yayında değil).";
                return RedirectToAction(nameof(Index));
            }

            return View(urun);
        }
    }
}
