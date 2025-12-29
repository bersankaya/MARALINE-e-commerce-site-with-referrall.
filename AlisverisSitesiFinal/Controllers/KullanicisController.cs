using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize]
    public class KullanicisController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;
        private readonly IOptions<ReferralConfig> _config;

        public KullanicisController(UygulamaDbContext context, UserManager<Kullanici> userManager, IOptions<ReferralConfig> config)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Kullanicis.ToListAsync());
        }

        public async Task<IActionResult> Details(string id)
        {

            if (string.IsNullOrEmpty(id))
                return NotFound();

            var kullanici = await _context.Kullanicis.FirstOrDefaultAsync(m => m.Id == id);
            if (kullanici == null)
                return NotFound();

            return View(kullanici);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Ad,Soyad,Email,ReferansKodu,SponsorId,ToplamHarcama,ToplamKazanilanPara,AylikKazanilanPara,KayitTarihi")] Kullanici kullanici)
        {
            if (ModelState.IsValid)
            {
                _context.Add(kullanici);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(kullanici);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var kullanici = await _context.Kullanicis.FindAsync(id);
            if (kullanici == null)
                return NotFound();

            return View(kullanici);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Ad,Soyad,Email,ReferansKodu,ToplamHarcama,ToplamKazanilanPara,AylikKazanilanPara,KayitTarihi")] Kullanici kullanici)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var userInDb = await _context.Kullanicis.FindAsync(id);
            if (userInDb == null)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    userInDb.Ad = kullanici.Ad;
                    userInDb.Soyad = kullanici.Soyad;
                    userInDb.Email = kullanici.Email;
                    userInDb.ReferansKodu = kullanici.ReferansKodu;
                    userInDb.ToplamHarcama = kullanici.ToplamHarcama;
                    userInDb.ToplamKazanilanPara = kullanici.ToplamKazanilanPara;
                    userInDb.AylikKazanilanPara = kullanici.AylikKazanilanPara;
                    userInDb.KayitTarihi = kullanici.KayitTarihi;

                    _context.Update(userInDb);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Kullanicis.Any(e => e.Id == id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(kullanici);
        }

        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var kullanici = await _context.Kullanicis.FirstOrDefaultAsync(m => m.Id == id);
            if (kullanici == null)
                return NotFound();

            return View(kullanici);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var kullanici = await _context.Kullanicis.FindAsync(id);
            if (kullanici != null)
            {
                _context.Kullanicis.Remove(kullanici);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> BenimReferanslarim()
        {
            var kullanici = await _userManager.GetUserAsync(User);
            if (kullanici == null) return NotFound();

            var referanslar = await _context.Users
                .Where(u => u.SponsorId == kullanici.Id)
                .ToListAsync();

            var bonusLoglar = await _context.BonusLoglari
                .Where(b => b.KullaniciId == kullanici.Id)
                .OrderByDescending(b => b.Tarih)
                .ToListAsync();

            foreach (var bonus in bonusLoglar)
            {
                if (!string.IsNullOrWhiteSpace(bonus.Aciklama) && bonus.Aciklama.Contains(" - "))
                {
                    bonus.Aciklama = bonus.Aciklama.Substring(0, bonus.Aciklama.LastIndexOf(" - "));
                }
                else if (bonus.Aciklama != null && bonus.Aciklama.Contains("Pair-"))
                {
                    int start = bonus.Aciklama.IndexOf(":") + 1;
                    int end = bonus.Aciklama.LastIndexOf(" - ");
                    if (start > 0 && end > start)
                    {
                        bonus.Aciklama = bonus.Aciklama.Substring(start, end - start).Trim();
                    }
                }
            }

            bool isAdmin = await _userManager.IsInRoleAsync(kullanici, "Admin");
            decimal earningCap = (!isAdmin || _config.Value.AdminHasEarningCap) ? _config.Value.EarningCap : 0;

            var model = new BenimReferanslarimViewModel
            {
                Kullanici = kullanici,
                Referanslar = referanslar,
                BonusLoglar = bonusLoglar,
                ToplamKazanilanPara = kullanici.ToplamKazanilanPara,
                EarningCap = earningCap
            };

            // Eğer ReferralTreeBuilder sınıfı varsa kullan:
            var treeBuilder = new ReferralTreeBuilder(_context);
            var tree = await treeBuilder.BuildTreeAsync(kullanici);
            if (tree == null || tree.Cocuklar.Count == 0)
            {
                // Fallback: Recursive metotla inşa et
                model.ReferralTreeRoot = await BuildReferralTreeAsync(kullanici);
            }
            else
            {
                model.ReferralTreeRoot = tree;
            }

            return View(model);
        }

        private async Task<ReferralNode> BuildReferralTreeAsync(Kullanici root)
        {
            var cocuklar = await _context.Users
                .Where(u => u.SponsorId == root.Id)
                .OrderBy(u => u.KayitTarihi)
                .ToListAsync();

            var node = new ReferralNode
            {
                AdSoyad = root.Ad + " " + root.Soyad,
                AktifMi = root.HasMetReferralThreshold,
                KayitTarihi = root.KayitTarihi.ToString("dd.MM.yyyy"),
                Cocuklar = new List<ReferralNode>()
            };

            foreach (var cocuk in cocuklar)
            {
                var altNode = await BuildReferralTreeAsync(cocuk);
                node.Cocuklar.Add(altNode);
            }

            return node;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRole(string userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var add = await _userManager.AddToRoleAsync(user, roleName);
            if (!add.Succeeded)
                return BadRequest(string.Join(" | ", add.Errors.Select(e => e.Description)));

            // KURAL: Admin veya Satici olduysa TC'yi temizle
            if (roleName == "Admin" || roleName == "Satici")
            {
                user.TcKimlikNo = null;  // null serbest çünkü Kullanici.cs'te string? olacak
                await _userManager.UpdateAsync(user);
            }

            return RedirectToAction("Details", new { id = userId });
        }

    }
}
