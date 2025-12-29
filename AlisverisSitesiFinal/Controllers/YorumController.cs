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
    [Authorize] // yorum atmak/düzenlemek/silmek için giriş şart
    public class YorumController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;

        public YorumController(UygulamaDbContext context, UserManager<Kullanici> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ****************************************************
        // CREATE (POST)  -> Details.cshtml formu buraya POST ediyor
        // ****************************************************
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int UrunId, int Puan, string? Icerik)
        {
            var urun = await _context.Uruns.AsNoTracking().FirstOrDefaultAsync(u => u.Id == UrunId);
            if (urun == null) return NotFound();

            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            // Basit doğrulamalar
            if (Puan < 0 || Puan > 5)
                ModelState.AddModelError(nameof(Puan), "Puan 0 ile 5 arasında olmalı.");
            if (string.IsNullOrWhiteSpace(Icerik))
                ModelState.AddModelError(nameof(Icerik), "Yorum metni boş olamaz.");

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Yorum kaydedilemedi. Lütfen alanları kontrol edin.";
                return RedirectToAction("Details", "Uruns", new { id = UrunId });
            }

            var yorum = new Yorum
            {
                UrunId = UrunId,
                KullaniciId = me.Id,
                Puan = Puan,
                Icerik = Icerik!.Trim(),
                Tarih = DateTime.Now
            };

            _context.Yorumlar.Add(yorum);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Yorumunuz kaydedildi. Teşekkürler!";
            return RedirectToAction("Details", "Uruns", new { id = UrunId });
        }

        // ****************************************************
        // EDIT (GET)  -> opsiyonel (yorum düzenleme sayfası)
        // ****************************************************
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var yorum = await _context.Yorumlar
                .Include(y => y.Kullanici)
                .FirstOrDefaultAsync(y => y.Id == id);
            if (yorum == null) return NotFound();

            if (!await CanManageAsync(yorum))
                return Forbid();

            return View(yorum); // Views/Yorum/Edit.cshtml (istersen sonra yazarım)
        }

        // ****************************************************
        // EDIT (POST)
        // ****************************************************
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, int Puan, string? Icerik)
        {
            var yorum = await _context.Yorumlar.FindAsync(id);
            if (yorum == null) return NotFound();

            if (!await CanManageAsync(yorum))
                return Forbid();

            if (Puan < 0 || Puan > 5)
                ModelState.AddModelError(nameof(Puan), "Puan 0 ile 5 arasında olmalı.");
            if (string.IsNullOrWhiteSpace(Icerik))
                ModelState.AddModelError(nameof(Icerik), "Yorum metni boş olamaz.");

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Güncelleme başarısız. Alanları kontrol edin.";
                return RedirectToAction("Details", "Uruns", new { id = yorum.UrunId });
            }

            yorum.Puan = Puan;
            yorum.Icerik = Icerik!.Trim();

            _context.Yorumlar.Update(yorum);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Yorum güncellendi.";
            return RedirectToAction("Details", "Uruns", new { id = yorum.UrunId });
        }

        // ****************************************************
        // DELETE (POST)
        // ****************************************************
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var yorum = await _context.Yorumlar.FindAsync(id);
            if (yorum == null) return NotFound();

            if (!await CanManageAsync(yorum))
                return Forbid();

            int urunId = yorum.UrunId;

            _context.Yorumlar.Remove(yorum);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Yorum silindi.";
            return RedirectToAction("Details", "Uruns", new { id = urunId });
        }

        // ****************************************************
        // Helper: bu yorumu yönetebilir mi? (Sahibi veya Admin)
        // ****************************************************
        private async Task<bool> CanManageAsync(Yorum y)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return false;

            if (y.KullaniciId == me.Id) return true;
            return User.IsInRole("Admin");
        }
    }
}
