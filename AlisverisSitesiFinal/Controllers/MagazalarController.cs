using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MagazalarController : Controller
    {
        private readonly UygulamaDbContext _context;
        public MagazalarController(UygulamaDbContext ctx) { _context = ctx; }

        public async Task<IActionResult> Index()
        {
            var list = await _context.Magazalar
                .Include(m => m.Sahip)
                .OrderByDescending(m => m.OlusturmaTarihi)
                .ToListAsync();
            return View(list);
        }

        // >>> GÜNCEL <<<
        public async Task<IActionResult> Details(int id)
        {
            var magaza = await _context.Magazalar
                .Include(m => m.Sahip)
                .Include(m => m.Urunler)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (magaza == null)
                return NotFound();

            // Özet kutuları
            ViewBag.ToplamUrun = magaza.Urunler?.Count ?? 0;
            ViewBag.YayindakiUrun = magaza.Urunler?.Count(u => u.YayindaMi) ?? 0;
            ViewBag.EnSonUrunTarihi = magaza.Urunler?
                .OrderByDescending(u => u.EklenmeTarihi)
                .FirstOrDefault()?.EklenmeTarihi;

            // Hangi sipariş durumları toplama dahil?
            var sayilanDurumlar = new[] { "Beklemede", "Onaylandı", "Kargolandı", "Tamamlandı" };

            // TOPLAM: JOIN ile, StoreId yoksa geçici olarak UserId üzerinden de say
            var toplamSaticiOdemesi = await (
                from sk in _context.SiparisKalemleri
                join s in _context.Siparisler on sk.SiparisId equals s.Id
                join u in _context.Uruns on sk.UrunId equals u.Id
                where (s.Durum != null && sayilanDurumlar.Contains(s.Durum))
                  && (u.StoreId == id || (u.StoreId == null && u.UserId == magaza.OwnerUserId))
                select (decimal)((u.SaticiTeklifFiyati ?? 0m) * sk.Miktar)
            ).SumAsync();

            // Teşhis amaçlı (istersen geçici göster)
            var matchingKalemSayisi = await (
                from sk in _context.SiparisKalemleri
                join s in _context.Siparisler on sk.SiparisId equals s.Id
                join u in _context.Uruns on sk.UrunId equals u.Id
                where (s.Durum != null && sayilanDurumlar.Contains(s.Durum))
                  && (u.StoreId == id || (u.StoreId == null && u.UserId == magaza.OwnerUserId))
                select 1
            ).CountAsync();

            var bosTeklifliKalem = await (
                from sk in _context.SiparisKalemleri
                join s in _context.Siparisler on sk.SiparisId equals s.Id
                join u in _context.Uruns on sk.UrunId equals u.Id
                where (s.Durum != null && sayilanDurumlar.Contains(s.Durum))
                  && (u.StoreId == id || (u.StoreId == null && u.UserId == magaza.OwnerUserId))
                  && (u.SaticiTeklifFiyati == null || u.SaticiTeklifFiyati == 0)
                select 1
            ).CountAsync();

            ViewBag.ToplamSaticiyaOdenecek = toplamSaticiOdemesi;
            ViewBag.MatchingKalemSayisi = matchingKalemSayisi;
            ViewBag.BosTeklifliKalem = bosTeklifliKalem;

            return View(magaza);
        }
    }
}
