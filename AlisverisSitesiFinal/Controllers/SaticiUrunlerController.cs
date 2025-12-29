using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Hosting; // IWebHostEnvironment
using Microsoft.AspNetCore.Http;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Satici,Admin")]
    public class SaticiUrunlerController : Controller
    {
        private readonly UygulamaDbContext _ctx;
        private readonly UserManager<Kullanici> _um;
        private readonly IWebHostEnvironment _env;

        public SaticiUrunlerController(UygulamaDbContext ctx, UserManager<Kullanici> um, IWebHostEnvironment env)
        {
            _ctx = ctx;
            _um = um;
            _env = env;
        }

        private async Task<(Kullanici? me, int? myStoreId)> TryGetMeAsync()
        {
            var me = await _um.GetUserAsync(User);
            if (me == null) return (null, null);

            var myStoreId = await _ctx.Magazalar
                .Where(x => x.OwnerUserId == me.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();

            return (me, myStoreId);
        }

        // SATICININ ÜRÜNLERİ
        public async Task<IActionResult> Index(string? q)
        {
            try
            {
                var (me, myStoreId) = await TryGetMeAsync();
                if (me == null)
                {
                    TempData["ErrorMessage"] = "Oturum bulunamadı.";
                    return RedirectToAction("Index", "Home");
                }

                var urunler = _ctx.Uruns
                    .Include(u => u.Kategori)
                    .Where(u => (u.UserId == me.Id) || (myStoreId != null && u.StoreId == myStoreId));

                if (!string.IsNullOrWhiteSpace(q))
                    urunler = urunler.Where(u => u.Ad.Contains(q) || u.Aciklama.Contains(q));

                var list = await urunler
                    .OrderByDescending(u => u.EklenmeTarihi)
                    .ToListAsync();

                return View(list); // Views/SaticiUrunler/Index.cshtml
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Mağaza ürünleri yüklenirken bir hata oluştu: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var (me, myStoreId) = await TryGetMeAsync();
                if (me == null) return Unauthorized();

                var urun = await _ctx.Uruns.FirstOrDefaultAsync(u => u.Id == id &&
                    (u.UserId == me.Id || (myStoreId != null && u.StoreId == myStoreId)));

                if (urun == null) return NotFound();

                ViewBag.Kategoriler = new SelectList(await _ctx.Kategoriler.OrderBy(x => x.Ad).ToListAsync(), "Id", "Ad", urun.KategoriId);
                return View(urun);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Sayfa yüklenirken bir hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Urun model,
            IFormFile? YeniResim,
            IFormFileCollection? EkResimler,
            [FromForm] List<string>? SilinecekEkGorseller)
        {
            try
            {
                var (me, myStoreId) = await TryGetMeAsync();
                if (me == null) return Unauthorized();

                var urun = await _ctx.Uruns.FirstOrDefaultAsync(u => u.Id == id &&
                    (u.UserId == me.Id || (myStoreId != null && u.StoreId == myStoreId)));
                if (urun == null) return NotFound();

                string Normalize(string? s) =>
                    string.Join(',', (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

                if (urun.RenkSecimiVar && string.IsNullOrWhiteSpace(model.RenkSecenekleri))
                    ModelState.AddModelError(nameof(model.RenkSecenekleri), "Renk seçeneklerini virgülle yazınız.");
                if (urun.BedenSecimiVar && string.IsNullOrWhiteSpace(model.BedenSecenekleri))
                    ModelState.AddModelError(nameof(model.BedenSecenekleri), "Beden seçeneklerini virgülle yazınız.");
                if (urun.BoyutSecimiVar && string.IsNullOrWhiteSpace(model.BoyutSecenekleri))
                    ModelState.AddModelError(nameof(model.BoyutSecenekleri), "Boyut seçeneklerini virgülle yazınız.");
                if (urun.NumaraSecimiVar && string.IsNullOrWhiteSpace(model.NumaraSecenekleri))
                    ModelState.AddModelError(nameof(model.NumaraSecenekleri), "Numara seçeneklerini virgülle yazınız.");
                if (urun.KapasiteSecimiVar && string.IsNullOrWhiteSpace(model.KapasiteSecenekleri))
                    ModelState.AddModelError(nameof(model.KapasiteSecenekleri), "Kapasite seçeneklerini virgülle yazınız.");

                if (!ModelState.IsValid)
                {
                    ViewBag.Kategoriler = new SelectList(await _ctx.Kategoriler.OrderBy(x => x.Ad).ToListAsync(), "Id", "Ad", model.KategoriId);
                    return View(model);
                }

                // Alan setleri
                urun.Ad = model.Ad;
                urun.Aciklama = model.Aciklama;
                urun.StokAdedi = model.StokAdedi;
                urun.KategoriId = model.KategoriId;

                urun.RenkSecimiVar = model.RenkSecimiVar;
                urun.BedenSecimiVar = model.BedenSecimiVar;
                urun.BoyutSecimiVar = model.BoyutSecimiVar;
                urun.NumaraSecimiVar = model.NumaraSecimiVar;
                urun.KapasiteSecimiVar = model.KapasiteSecimiVar;

                urun.RenkSecenekleri = Normalize(model.RenkSecenekleri);
                urun.BedenSecenekleri = Normalize(model.BedenSecenekleri);
                urun.BoyutSecenekleri = Normalize(model.BoyutSecenekleri);
                urun.NumaraSecenekleri = Normalize(model.NumaraSecenekleri);
                urun.KapasiteSecenekleri = Normalize(model.KapasiteSecenekleri);
                urun.MateryalSecenekleri = Normalize(model.MateryalSecenekleri);
                urun.DesenSecenekleri = Normalize(model.DesenSecenekleri);

                // SATICI fiyat teklifi -> Admin onayı gerekir
                urun.SaticiTeklifFiyati = model.SaticiTeklifFiyati;
                urun.YayindaMi = false;
                urun.Durum = UrunDurum.AdminFiyatBekliyor;

                // ---- Dosya yolları
                var urunlerKlasor = Path.Combine(_env.WebRootPath, "images", "urunler");
                Directory.CreateDirectory(urunlerKlasor);

                // Ana resmin kök adı (stem)
                string? stem = null;

                // Ana resim değişiyorsa
                if (YeniResim != null && YeniResim.Length > 0)
                {
                    // Eski ana resmi sil
                    if (!string.IsNullOrEmpty(urun.ResimUrl))
                    {
                        var eskiYol = Path.Combine(_env.WebRootPath, urun.ResimUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(eskiYol)) System.IO.File.Delete(eskiYol);
                    }

                    // Yeni ana resim
                    var ext = Path.GetExtension(YeniResim.FileName);
                    stem = Guid.NewGuid().ToString("N");
                    var anaAd = stem + ext;
                    using (var fs = System.IO.File.Create(Path.Combine(urunlerKlasor, anaAd)))
                        await YeniResim.CopyToAsync(fs);

                    urun.ResimUrl = "/images/urunler/" + anaAd;
                }
                else
                {
                    // Ana resmi değiştirmediyse mevcut stem'i bul
                    if (!string.IsNullOrWhiteSpace(urun.ResimUrl))
                        stem = Path.GetFileNameWithoutExtension(urun.ResimUrl);
                }

                // ---- Seçilen ek görselleri sil
                if (SilinecekEkGorseller != null && SilinecekEkGorseller.Count > 0)
                {
                    foreach (var rel in SilinecekEkGorseller.Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        try
                        {
                            var fiziksel = Path.Combine(_env.WebRootPath, rel.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                            if (System.IO.File.Exists(fiziksel))
                                System.IO.File.Delete(fiziksel);
                        }
                        catch
                        {
                            // loglamak istersen Logger enjekte edip yazabilirsin
                        }
                    }
                }

                // ---- Yeni ek görselleri kaydet
                if (!string.IsNullOrEmpty(stem) && EkResimler != null && EkResimler.Count > 0)
                {
                    // Ekleri aynı klasöre, stem_2, stem_3 ... şeklinde yazalım
                    // Mevcut en büyük index'i bul
                    int nextIndex = 2;
                    try
                    {
                        string? anaFiz = null;
                        if (!string.IsNullOrEmpty(urun.ResimUrl))
                            anaFiz = Path.Combine(_env.WebRootPath, urun.ResimUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                        var dir = !string.IsNullOrEmpty(anaFiz) ? Path.GetDirectoryName(anaFiz!) : urunlerKlasor;
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            var mevcutIndexler = Directory.GetFiles(dir)
                                .Select(p => Path.GetFileNameWithoutExtension(p))
                                .Where(name => name.StartsWith(stem + "_", StringComparison.OrdinalIgnoreCase)
                                            || name.StartsWith(stem + "-", StringComparison.OrdinalIgnoreCase)
                                            || name.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase))
                                .Select(name =>
                                {
                                    var tail = name.Substring(stem.Length + 1);
                                    return int.TryParse(tail, out var n) ? n : (int?)null;
                                })
                                .Where(n => n.HasValue)
                                .Select(n => n!.Value)
                                .ToList();

                            if (mevcutIndexler.Any())
                                nextIndex = mevcutIndexler.Max() + 1;
                        }
                    }
                    catch { /* sessiz geç */ }

                    foreach (var f in EkResimler.Where(f => f != null && f.Length > 0 && f.ContentType.StartsWith("image/")).Take(6))
                    {
                        var ext = Path.GetExtension(f!.FileName);
                        var ad = $"{stem}_{nextIndex}{ext}";
                        using var fsEk = System.IO.File.Create(Path.Combine(urunlerKlasor, ad));
                        await f.CopyToAsync(fsEk);
                        nextIndex++;
                    }
                }

                await _ctx.SaveChangesAsync();
                TempData["SuccessMessage"] = "Ürün güncellendi (admin onayı bekleniyor).";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ürün güncellenirken bir hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var (me, myStoreId) = await TryGetMeAsync();
                if (me == null) return Unauthorized();

                var urun = await _ctx.Uruns.FirstOrDefaultAsync(u => u.Id == id &&
                    (u.UserId == me.Id || (myStoreId != null && u.StoreId == myStoreId)));
                if (urun == null)
                {
                    TempData["ErrorMessage"] = "Ürün bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                // Ürüne bağlı siparişler (kalem -> sipariş)
                var relatedOrderIds = await _ctx.SiparisKalemleri
                    .Where(k => k.UrunId == id)
                    .Select(k => k.SiparisId)
                    .Distinct()
                    .ToListAsync();

                if (relatedOrderIds.Any())
                {
                    var durumlar = await _ctx.Siparisler
                        .Where(s => relatedOrderIds.Contains(s.Id))
                        .Select(s => s.Durum)  // Enum ise ToString() ile bellekte kontrol edeceğiz
                        .ToListAsync();

                    static bool IsCompleted(object? durum)
                    {
                        var t = (durum?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                        // Türkçe + olası İngilizce varyasyonları
                        return t.Contains("tamam") || t == "completed" || t.Contains("complete");
                    }

                    var allCompleted = durumlar.All(IsCompleted);
                    if (!allCompleted)
                    {
                        TempData["ErrorMessage"] =
                            "Bu ürün tamamlanmamış siparişlerde yer alıyor (Beklemede/Onaylandı/Hazırlanıyor/Kargoda vb.). Bu nedenle silinemez. Lütfen ürünü yayından kaldırın.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                // Görselleri temizle (ana + ekler)
                if (!string.IsNullOrWhiteSpace(urun.ResimUrl))
                {
                    var fizikselYol = Path.Combine(_env.WebRootPath,
                        urun.ResimUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(fizikselYol)) System.IO.File.Delete(fizikselYol);

                    var stem = Path.GetFileNameWithoutExtension(urun.ResimUrl);
                    var dir = Path.GetDirectoryName(fizikselYol);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        foreach (var p in Directory.GetFiles(dir).Where(p =>
                        {
                            var name = Path.GetFileNameWithoutExtension(p);
                            return name.StartsWith(stem + "_", StringComparison.OrdinalIgnoreCase)
                                   || name.StartsWith(stem + "-", StringComparison.OrdinalIgnoreCase)
                                   || name.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase);
                        }))
                        {
                            try { System.IO.File.Delete(p); } catch { /* loglamak istersen yutma */ }
                        }
                    }
                }

                _ctx.Uruns.Remove(urun);
                await _ctx.SaveChangesAsync();

                TempData["SuccessMessage"] = "Ürün silindi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ürün silinirken bir hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

    }
}
