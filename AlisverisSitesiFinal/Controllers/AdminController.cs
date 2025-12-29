using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;
        private readonly ReferralBonusHandler _bonusHandler;

        public AdminController(
            UygulamaDbContext context,
            UserManager<Kullanici> userManager,
            ReferralBonusHandler bonusHandler)
        {
            _context = context;
            _userManager = userManager;
            _bonusHandler = bonusHandler;
        }

        public async Task<IActionResult> Index()
        {
            var bonuslar = await _context.BonusLoglari
                .OrderByDescending(b => b.Tarih)
                .Take(50)
                .ToListAsync();

            var enCokKazananlar = await _context.Users
                .Where(u => u.ToplamKazanilanPara > 0)
                .OrderByDescending(u => u.ToplamKazanilanPara)
                .Take(10)
                .ToListAsync();

            ViewBag.EnCokKazananlar = enCokKazananlar;

            // ✅ Müşteriye dağıtılan = onaylanmış para çekme taleplerinin toplamı
            var musteriyeDagitilan = await _context.ParaCekmeTalepleri
                .Where(t => t.OnaylandiMi && !t.ReddedildiMi)
                .SumAsync(t => (decimal?)t.Tutar) ?? 0m;

            // ✅ Bekleyen talepler
            var bekleyenCekim = await _context.ParaCekmeTalepleri
                .Where(t => !t.OnaylandiMi && !t.ReddedildiMi)
                .SumAsync(t => (decimal?)t.Tutar) ?? 0m;

            ViewBag.MusteriyeDagitilan = musteriyeDagitilan;
            ViewBag.BekleyenCekim = bekleyenCekim;
            return View(bonuslar);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BonuslariSifirlaVeGuncelle(
     [FromServices] ReferralBonusHandler bonusHandler)
        {
            // Motor bonuslarını yeniden üretir, AylikKazanilanPara'ya DOKUNMAZ,
            // pasif ödeme loglarını SİLMEZ, cüzdanı loglardan yeniden hesaplar.
            await bonusHandler.RecalculateAllEarningsAsync();

            TempData["SuccessMessage"] = "✅ Bonuslar temizlendi ve güncellendi.";
            return RedirectToAction(nameof(Index));
        }



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

    }
}
