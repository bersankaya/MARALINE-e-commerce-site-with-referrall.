using System;
using System.Linq;
using System.Threading.Tasks;
using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize]
    [Route("Adresler")]
    public class AdreslerController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;

        public AdreslerController(UygulamaDbContext context, UserManager<Kullanici> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<Kullanici> CurrentUser()
        {
            var u = await _userManager.GetUserAsync(User);
            if (u == null) throw new Exception("Kullanıcı bulunamadı.");
            return u;
        }
        private string? CurrentUserId() => _userManager.GetUserId(User);

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var user = await CurrentUser();

            var list = await _context.Adresler
                .Where(a => a.KullaniciId == user.Id)
                .OrderByDescending(a => a.IsVarsayilan)
                .ThenBy(a => a.AdresBasligi)
                .ToListAsync();

            var kullanilanAdresSayilari = await _context.Siparisler
                .Where(s => s.IdentityUserId == user.Id && s.AdresId != null)
                .GroupBy(s => s.AdresId!.Value)
                .Select(g => new { AdresId = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.AdresKullanimSayilari = kullanilanAdresSayilari
                .ToDictionary(x => x.AdresId, x => x.Count);

            return View(list);
        }

        [HttpGet("Create")]
        public IActionResult Create(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new Adres());
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Adres model, string? returnUrl = null)
        {
            var userId = CurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            model.KullaniciId = userId;
            ModelState.Remove(nameof(Adres.KullaniciId));
            ModelState.Remove(nameof(Adres.Kullanici));

            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            bool hasAny = await _context.Adresler.AnyAsync(a => a.KullaniciId == userId);
            if (!hasAny) model.IsVarsayilan = true;

            // ✅ TCKimlik boş bırakılmışsa null olarak kaydet (zorunlu değilse)
            if (string.IsNullOrWhiteSpace(model.TCKimlik))
                model.TCKimlik = null;

            _context.Adresler.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Adres başarıyla kaydedildi.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await CurrentUser();
            var adres = await _context.Adresler.FirstOrDefaultAsync(a => a.Id == id && a.KullaniciId == user.Id);
            if (adres == null) return NotFound();
            return View(adres);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Adres model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var adres = await _context.Adresler.FirstOrDefaultAsync(a => a.Id == id && a.KullaniciId == user.Id);
            if (adres == null) return NotFound();

            ModelState.Remove(nameof(Adres.KullaniciId));
            ModelState.Remove(nameof(Adres.Kullanici));

            if (!ModelState.IsValid) return View(model);

            adres.AdresBasligi = model.AdresBasligi;
            adres.AdresDetayi = model.AdresDetayi;
            adres.Il = model.Il;
            adres.Ilce = model.Ilce;
            adres.PostaKodu = model.PostaKodu;
            adres.Telefon = model.Telefon;
            adres.TCKimlik = string.IsNullOrWhiteSpace(model.TCKimlik) ? null : model.TCKimlik; // ✅ EKLENDİ

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Adres güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Sil")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id)
        {
            var user = await CurrentUser();
            var adres = await _context.Adresler.FirstOrDefaultAsync(a => a.Id == id && a.KullaniciId == user.Id);
            if (adres == null) return NotFound();

            bool kullanilan = await _context.Siparisler.AnyAsync(s => s.IdentityUserId == user.Id && s.AdresId == id);
            if (kullanilan)
            {
                TempData["ErrorMessage"] = "Bu adres geçmiş siparişlerde kullanıldığı için silinemez. Lütfen başka bir adresi varsayılan yapın ve bu adresi listede tutun.";
                return RedirectToAction(nameof(Index));
            }

            bool varsayilanMi = adres.IsVarsayilan;
            _context.Adresler.Remove(adres);
            await _context.SaveChangesAsync();

            if (varsayilanMi)
            {
                var kalan = await _context.Adresler
                    .Where(a => a.KullaniciId == user.Id)
                    .OrderBy(a => a.Id)
                    .FirstOrDefaultAsync();

                if (kalan != null)
                {
                    kalan.IsVarsayilan = true;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMessage"] = "Adres silindi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("VarsayilanYap")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VarsayilanYap(int id)
        {
            var user = await CurrentUser();
            var hepsi = await _context.Adresler.Where(a => a.KullaniciId == user.Id).ToListAsync();
            foreach (var a in hepsi) a.IsVarsayilan = false;

            var hedef = hepsi.FirstOrDefault(a => a.Id == id);
            if (hedef == null) return NotFound();

            hedef.IsVarsayilan = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Varsayılan adres ayarlandı.";
            return RedirectToAction(nameof(Index));
        }
    }
}
