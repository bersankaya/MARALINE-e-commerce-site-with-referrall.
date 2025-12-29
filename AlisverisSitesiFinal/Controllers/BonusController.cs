using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize]
    public class BonusLogController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;

        public BonusLogController(UygulamaDbContext context, UserManager<Kullanici> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /BonusLog/KullaniciBonusGecmisi
        public async Task<IActionResult> KullaniciBonusGecmisi()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Bonus hareketleri
            var logs = await _context.BonusLoglari
                .Where(b => b.KullaniciId == user.Id)
                .OrderByDescending(b => b.Tarih)
                .ToListAsync();

            // Ömür boyu kazanılan (BonusLog’da pozitifler)
            var toplamKazanilan = logs.Where(x => x.Tutar > 0).Sum(x => x.Tutar);

            // TOPLAM ÇEKİLEN: Para çekme talepleri (Yalnızca onaylananlar)
            // DbSet adı projende farklıysa (örn. ParaCekmeTalebi, ParaCekmeTalepleri) aşağıyı ona göre düzelt.
            decimal toplamCekilen = 0m;
            if (_context.Set<ParaCekmeTalebi>() != null)
            {
                toplamCekilen = await _context.Set<ParaCekmeTalebi>()
                    .Where(t => t.KullaniciId == user.Id && t.OnaylandiMi)
                    .SumAsync(t => (decimal?)t.Tutar) ?? 0m;
            }

            // GÜNCEL BAKİYE: AspNetUsers.ToplamKazanc (gerçek bakiye, çekim onaylarında düşen)
            ViewBag.GuncelBakiye = user.ToplamKazanc;
            ViewBag.ToplamKazanc = toplamKazanilan;
            ViewBag.ToplamCekilen = toplamCekilen;

            return View(logs); // Views/BonusLog/KullaniciBonusGecmisi.cshtml
        }
    }
}
