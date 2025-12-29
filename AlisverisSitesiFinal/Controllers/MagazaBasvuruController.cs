using System.Linq;
using System.Threading.Tasks;
using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlisverisSitesiFinal.Controllers
{
    public class MagazaBasvuruController : Controller
    {
        private readonly UygulamaDbContext _ctx;
        private readonly UserManager<Kullanici> _userManager;
        private readonly SignInManager<Kullanici> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<MagazaBasvuruController> _logger;

        public MagazaBasvuruController(
            UygulamaDbContext ctx,
            UserManager<Kullanici> userManager,
            SignInManager<Kullanici> signInManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<MagazaBasvuruController> logger)
        {
            _ctx = ctx;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        // ===========================
        // 1) MAĞAZA OL (Anonim Kayıt)
        // ===========================
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Kayit()
        {
            return View(new MagazaKayitVM());
        }

        [AllowAnonymous]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Kayit(MagazaKayitVM m)
        {
            if (!ModelState.IsValid) return View(m);

            var requestId = HttpContext?.TraceIdentifier;

            try
            {
                // 1) Kullanıcı oluştur
                var user = new Kullanici
                {
                    UserName = m.Email,
                    Email = m.Email,
                    EmailConfirmed = true,
                    Ad = m.Ad,
                    Soyad = m.Soyad
                };

                var createRes = await _userManager.CreateAsync(user, m.Sifre);
                if (!createRes.Succeeded)
                {
                    foreach (var e in createRes.Errors) ModelState.AddModelError("", e.Description);
                    return View(m);
                }

                // 2) Rol: SaticiAday
                if (!await _roleManager.RoleExistsAsync("SaticiAday"))
                    await _roleManager.CreateAsync(new IdentityRole("SaticiAday"));
                await _userManager.AddToRoleAsync(user, "SaticiAday");

                // 3) Başvuru kaydı
                var basvuru = new MagazaBasvurusu
                {
                    KullaniciId = user.Id,
                    MagazaAdi = m.MagazaAdi,
                    VergiNo = m.VergiNo,
                    IBAN = m.IBAN,
                    Aciklama = m.Aciklama,
                    Durum = BasvuruDurum.Beklemede
                };
                _ctx.MagazaBasvurulari.Add(basvuru);
                await _ctx.SaveChangesAsync();

                // 4) Giriş yaptır ve Satıcı Paneli'ne yönlendir
                await _signInManager.SignInAsync(user, isPersistent: true);
                TempData["SuccessMessage"] = "Başvurunuz alındı. Onaydan sonra satışa başlayabilirsiniz.";
                return RedirectToAction("Index", "SaticiPanel");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex,
                    "Hata (Kayit POST). RequestId: {RequestId}, Email: {Email}",
                    requestId, m?.Email);

                ModelState.AddModelError(string.Empty, "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
                TempData["ErrorMessage"] = $"İşlem sırasında bir hata oluştu. İstek No: {requestId}";
                return View(m);
            }
        }

        // =================================================
        // 2) Girişli kullanıcı için sadece Başvuru bırakma
        // =================================================
        [Authorize]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new MagazaBasvurusu());
        }

        [Authorize]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MagazaBasvurusu model)
        {
            var requestId = HttpContext?.TraceIdentifier;

            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                if (!ModelState.IsValid) return View(model);

                model.KullaniciId = me.Id;
                model.Durum = BasvuruDurum.Beklemede;

                _ctx.MagazaBasvurulari.Add(model);
                await _ctx.SaveChangesAsync();

                // Kullanıcıyı SaticiAday rolüne al (yoksa)
                if (!await _roleManager.RoleExistsAsync("SaticiAday"))
                    await _roleManager.CreateAsync(new IdentityRole("SaticiAday"));
                if (!await _userManager.IsInRoleAsync(me, "SaticiAday"))
                    await _userManager.AddToRoleAsync(me, "SaticiAday");

                TempData["SuccessMessage"] = "Başvurunuz alındı. Onaydan sonra 'Satıcı Paneli' açılacaktır.";
                return RedirectToAction("Index", "SaticiPanel");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex,
                    "Hata (Create POST). RequestId: {RequestId}, KullaniciId: {KullaniciId}",
                    requestId, model?.KullaniciId);

                ModelState.AddModelError(string.Empty, "Başvuru oluşturulurken beklenmeyen bir hata oluştu.");
                TempData["ErrorMessage"] = $"İşlem sırasında bir hata oluştu. İstek No: {requestId}";
                return View(model);
            }
        }

        // ================
        // 3) ADMIN İşlemleri
        // ================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Liste()
        {
            var requestId = HttpContext?.TraceIdentifier;

            try
            {
                var list = await _ctx.MagazaBasvurulari
                    .OrderByDescending(x => x.BasvuruTarihi)
                    .ToListAsync();
                return View(list);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Hata (Liste GET). RequestId: {RequestId}", requestId);
                TempData["ErrorMessage"] = $"Başvurular listelenirken hata oluştu. İstek No: {requestId}";
                // Boş liste ile sayfayı açalım ki UI göçmesin
                return View(Enumerable.Empty<MagazaBasvurusu>());
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Onayla(int id)
        {
            var requestId = HttpContext?.TraceIdentifier;

            try
            {
                var basvuru = await _ctx.MagazaBasvurulari.FindAsync(id);
                if (basvuru == null) return NotFound();

                basvuru.Durum = BasvuruDurum.Onaylandi;
                await _ctx.SaveChangesAsync();

                var user = await _userManager.FindByIdAsync(basvuru.KullaniciId);
                if (user != null)
                {
                    if (!await _roleManager.RoleExistsAsync("Satici"))
                        await _roleManager.CreateAsync(new IdentityRole("Satici"));

                    if (await _userManager.IsInRoleAsync(user, "SaticiAday"))
                        await _userManager.RemoveFromRoleAsync(user, "SaticiAday");

                    if (!await _userManager.IsInRoleAsync(user, "Satici"))
                        await _userManager.AddToRoleAsync(user, "Satici");
                }

                TempData["SuccessMessage"] = "Başvuru onaylandı ve kullanıcı 'Satici' rolüne alındı.";
                return RedirectToAction(nameof(Liste));
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex,
                    "Hata (Onayla POST). RequestId: {RequestId}, BasvuruId: {BasvuruId}",
                    requestId, id);

                TempData["ErrorMessage"] = $"Onay işlemi sırasında hata oluştu. İstek No: {requestId}";
                return RedirectToAction(nameof(Liste));
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reddet(int id, string? not)
        {
            var requestId = HttpContext?.TraceIdentifier;

            try
            {
                var basvuru = await _ctx.MagazaBasvurulari.FindAsync(id);
                if (basvuru == null) return NotFound();

                basvuru.Durum = BasvuruDurum.Reddedildi;
                basvuru.Aciklama = not;
                await _ctx.SaveChangesAsync();

                TempData["Warning"] = "Başvuru reddedildi.";
                return RedirectToAction(nameof(Liste));
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex,
                    "Hata (Reddet POST). RequestId: {RequestId}, BasvuruId: {BasvuruId}",
                    requestId, id);

                TempData["ErrorMessage"] = $"Reddetme işlemi sırasında hata oluştu. İstek No: {requestId}";
                return RedirectToAction(nameof(Liste));
            }
        }
    }
}
