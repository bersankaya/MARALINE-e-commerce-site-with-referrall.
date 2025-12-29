using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminUrunOnayController : Controller
    {
        private readonly UygulamaDbContext _context;
        public AdminUrunOnayController(UygulamaDbContext context) { _context = context; }

        // Fiyat bekleyen ürünler listesi
        public async Task<IActionResult> Bekleyenler()
        {
            var list = await _context.Uruns
                .Where(x => x.Durum == UrunDurum.AdminFiyatBekliyor || (!x.YayindaMi && x.Durum == UrunDurum.Taslak))
                .OrderByDescending(x => x.EklenmeTarihi)
                .ToListAsync();

            return View(list);
        }

        // Fiyatlandırma sayfası
        public async Task<IActionResult> Fiyatlandir(int id)
        {
            var urun = await _context.Uruns
                .Include(u => u.Kategori)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (urun == null) return NotFound();
            return View(urun);
        }

        // Tek fiyat sistemi: sadece Admin Fiyatı
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Fiyatlandir(int id, decimal? fiyatAdmin, bool yayinaAl = true)
        {
            var urun = await _context.Uruns.FindAsync(id);
            if (urun == null) return NotFound();

            // Admin fiyatı zorunlu
            if (!fiyatAdmin.HasValue || fiyatAdmin.Value <= 0)
            {
                TempData["Error"] = "Admin fiyatı zorunludur ve 0'dan büyük olmalıdır.";
                return RedirectToAction(nameof(Fiyatlandir), new { id });
            }

            // Tek fiyat: Fiyat = Admin Fiyatı
            urun.FiyatAdmin = fiyatAdmin.Value;
            urun.Fiyat = fiyatAdmin.Value;

            // Referanslı fiyat artık kullanılmıyor
            urun.FiyatReferansli = null;

            // Yayın durumu
            if (yayinaAl)
            {
                urun.YayindaMi = true;
                urun.Durum = UrunDurum.Yayinda;
            }
            else
            {
                urun.YayindaMi = false;
                urun.Durum = UrunDurum.AdminFiyatBekliyor;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = yayinaAl
                ? "Ürün fiyatlandırıldı ve yayına alındı."
                : "Ürün fiyatlandırıldı. Yayına alınmadı (beklemede).";

            // Listeye dönmek istersen:
            return RedirectToAction(nameof(Bekleyenler));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reddet(int id, string? aciklama)
        {
            var urun = await _context.Uruns.FindAsync(id);
            if (urun == null) return NotFound();

            urun.YayindaMi = false;
            urun.Durum = UrunDurum.Reddedildi;

            await _context.SaveChangesAsync();
            TempData["Warning"] = "Ürün reddedildi.";
            return RedirectToAction(nameof(Bekleyenler));
        }
    }
}
