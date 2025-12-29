using System;
using System.Linq;
using System.Threading.Tasks;
using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Models.ViewModels;
using AlisverisSitesiFinal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminOdemelerController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly ReferralBonusHandler _bonusHandler;
        private readonly IOptions<ReferralConfig> _config;

        public AdminOdemelerController(
            UygulamaDbContext context,
            ReferralBonusHandler bonusHandler,
            IOptions<ReferralConfig> config)
        {
            _context = context;
            _bonusHandler = bonusHandler;
            _config = config;
        }

        private static string DurumStr(bool onay, bool red)
            => onay ? "Onaylandi" : red ? "Reddedildi" : "Beklemede";

        public async Task<IActionResult> Index(string? q)
        {
            var users = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim().ToLower();
                users = users.Where(u =>
                    (((u.Ad ?? "") + " " + (u.Soyad ?? "")).ToLower().Contains(q)) ||
                    ((u.Email ?? "").ToLower().Contains(q)) ||
                    ((u.UserName ?? "").ToLower().Contains(q)) ||
                    (((u.IBAN ?? "").Replace(" ", "")).ToLower().Contains(q))
                );
            }

            var vmList = await users
                .Select(u => new OdemeDagitimViewModel
                {
                    UserId = u.Id,
                    AdSoyad = (u.Ad ?? "") + " " + (u.Soyad ?? ""),
                    IBAN = u.IBAN,
                    ToplamKazanc = u.ToplamKazanc,
                    AylikKazanilanPara = u.AylikKazanilanPara,

                    SonTalepId = _context.ParaCekmeTalepleri
                        .Where(t => t.KullaniciId == u.Id)
                        .OrderByDescending(t => t.TalepTarihi)
                        .Select(t => (int?)t.Id)
                        .FirstOrDefault(),

                    SonTalepTutar = _context.ParaCekmeTalepleri
                        .Where(t => t.KullaniciId == u.Id)
                        .OrderByDescending(t => t.TalepTarihi)
                        .Select(t => (decimal?)t.Tutar)
                        .FirstOrDefault(),

                    SonTalepDurum = _context.ParaCekmeTalepleri
                        .Where(t => t.KullaniciId == u.Id)
                        .OrderByDescending(t => t.TalepTarihi)
                        .Select(t => DurumStr(t.OnaylandiMi, t.ReddedildiMi))
                        .FirstOrDefault(),

                    SonTalepTarihi = _context.ParaCekmeTalepleri
                        .Where(t => t.KullaniciId == u.Id)
                        .OrderByDescending(t => t.TalepTarihi)
                        .Select(t => (DateTime?)t.TalepTarihi)
                        .FirstOrDefault()
                })
                .OrderByDescending(x => x.ToplamKazanc)
                .Take(200)
                .ToListAsync();

            return View(vmList);
        }

        // PASİF DAĞIT: AylikKazanilanPara -> ToplamKazanc ; sonra sıfırla (ayda bir)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalistirAylikPasif()
        {
            var adet = await _bonusHandler.DistributeMonthlyPassiveAsync();
            TempData["SuccessMessage"] = $"Aylık pasif gelir dağıtımı tamamlandı. ({adet} kullanıcı)";
            return RedirectToAction(nameof(Index));
        }

        // (YENİ) AYLIK YÜKLEME: her ayın 1'inde CAP kadar AylikKazanilanPara'ya yükle (ayda bir)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AylikPasifiYukle()
        {
            var adet = await _bonusHandler.RefillMonthlyPassiveAsync();
            TempData["SuccessMessage"] = $"Aylık pasif gelir YÜKLEMESİ tamamlandı. ({adet} kullanıcı)";
            return RedirectToAction(nameof(Index));
        }

        // Mevcut diğer aksiyonlar (talep onaylama, reddetme, manuel ödeme vb.) aynen korunur…
        // ...
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TalepOnayla(int id)
        {
            var talep = await _context.ParaCekmeTalepleri
                .FirstOrDefaultAsync(t => t.Id == id);

            if (talep == null)
            {
                TempData["ErrorMessage"] = "Talep bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            // zaten onaylıysa dokunma
            if (!talep.OnaylandiMi)
            {
                talep.OnaylandiMi = true;
                talep.OnayTarihi = DateTime.Now;

                _context.ParaCekmeTalepleri.Update(talep);
                await _context.SaveChangesAsync();

                // Cüzdan görünümü ToplamKazanilanPara - OnaylananÇekimler olarak hesaplanıyorsa
                // anında senkron için kullanıcı cüzdanını yeniden hesaplayalım:
                var u = await _context.Users.FirstOrDefaultAsync(x => x.Id == talep.KullaniciId);
                if (u != null)
                {
                    var totalEngine = await _context.BonusLoglari
                        .Where(b => b.KullaniciId == u.Id &&
                                    (b.Aciklama.StartsWith("Doğrudan bonus - pair-") ||
                                     EF.Functions.Like(b.Aciklama, "% seviye zincir bonus - %") ||
                                     b.Aciklama.StartsWith("Aylık pasif gelir ödemesi")))
                        .SumAsync(b => (decimal?)b.Tutar) ?? 0m;

                    var approvedWithdrawals = await _context.ParaCekmeTalepleri
                        .Where(x => x.KullaniciId == u.Id && x.OnaylandiMi)
                        .SumAsync(x => (decimal?)x.Tutar) ?? 0m;

                    u.ToplamKazanilanPara = totalEngine;
                    u.ToplamKazanc = Math.Max(0, totalEngine - approvedWithdrawals);
                    _context.Users.Update(u);
                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMessage"] = "Para çekme talebi onaylandı.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TalepReddet(int id)
        {
            var talep = await _context.ParaCekmeTalepleri
                .FirstOrDefaultAsync(t => t.Id == id);

            if (talep == null)
            {
                TempData["ErrorMessage"] = "Talep bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            // onaylı bir talebi reddetmeye dönüştürmüyoruz; sadece Onaylandı=false kalsın
            talep.OnaylandiMi = false;
            talep.OnayTarihi = null;

            _context.ParaCekmeTalepleri.Update(talep);
            await _context.SaveChangesAsync();

            // Cüzdanı tekrar hesapla (onay geri alındıysa cüzdan artar)
            var u = await _context.Users.FirstOrDefaultAsync(x => x.Id == talep.KullaniciId);
            if (u != null)
            {
                var totalEngine = await _context.BonusLoglari
                    .Where(b => b.KullaniciId == u.Id &&
                                (b.Aciklama.StartsWith("Doğrudan bonus - pair-") ||
                                 EF.Functions.Like(b.Aciklama, "% seviye zincir bonus - %") ||
                                 b.Aciklama.StartsWith("Aylık pasif gelir ödemesi")))
                    .SumAsync(b => (decimal?)b.Tutar) ?? 0m;

                var approvedWithdrawals = await _context.ParaCekmeTalepleri
                    .Where(x => x.KullaniciId == u.Id && x.OnaylandiMi)
                    .SumAsync(x => (decimal?)x.Tutar) ?? 0m;

                u.ToplamKazanilanPara = totalEngine;
                u.ToplamKazanc = Math.Max(0, totalEngine - approvedWithdrawals);
                _context.Users.Update(u);
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Para çekme talebi reddedildi.";
            return RedirectToAction(nameof(Index));
        }
    }
}
