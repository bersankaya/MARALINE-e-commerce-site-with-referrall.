using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;
using System.IO;

namespace AlisverisSitesiFinal.Controllers
{
    public class UrunsController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;
        private readonly ReferralBonusHandler _bonusHandler;
        private bool IsSafeImage(IFormFile f, long maxBytes = 2_000_000)
        {
            if (f == null || f.Length == 0 || f.Length > maxBytes) return false;
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext)) return false;
            return f.ContentType.StartsWith("image/");
        }

        public UrunsController(UygulamaDbContext context,
                               UserManager<Kullanici> userManager,
                               IOptions<ReferralConfig> config)
        {
            _context = context;
            _userManager = userManager;
            _bonusHandler = new ReferralBonusHandler(context, userManager, config);
        }

        // LISTE (Server-side Pagination)
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? filtre, int? kategoriId, string? arama, int page = 1, int pageSize = 24)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize is < 1 or > 96) pageSize = 24;

                var q = _context.Uruns
                    .AsNoTracking()
                    .Where(u =>
                        u.YayindaMi &&
                        u.Durum == UrunDurum.Yayinda &&
                        _context.Magazalar.Any(m => m.Id == u.StoreId && m.AktifMi));

                if (kategoriId.HasValue)
                    q = q.Where(u => u.KategoriId == kategoriId.Value);

                if (!string.IsNullOrEmpty(filtre))
                {
                    if (filtre == "populer") q = q.Where(u => u.IsPopular);
                    if (filtre == "avantajli") q = q.Where(u => u.IsAvantajli);
                    if (filtre == "coksatan")
                    {
                        var topIds = await _context.SiparisKalemleri
                            .Where(sk => sk.Siparis != null && sk.Siparis.Durum == "Onaylandı")
                            .GroupBy(k => k.UrunId)
                            .Select(g => new { UrunId = g.Key, Toplam = g.Sum(x => x.Miktar) })
                            .OrderByDescending(x => x.Toplam)
                            .Take(100)
                            .Select(x => x.UrunId)
                            .ToListAsync();

                        q = q.Where(u => u.IsCokSatan || topIds.Contains(u.Id));
                    }
                }

                if (!string.IsNullOrWhiteSpace(arama))
                {
                    var term = arama.Trim();
                    q = q.Where(u =>
                        EF.Functions.Like(u.Ad, $"%{term}%") ||
                        EF.Functions.Like(u.Aciklama!, $"%{term}%"));
                }

                // toplam sayım
                var totalCount = await q.CountAsync();

                // sayfa taşarsa son sayfaya çek
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                if (totalPages > 0 && page > totalPages) page = totalPages;

                // sayfa verisi
                var items = await q
                    .OrderByDescending(u => u.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var vm = new UrunListViewModel
                {
                    TumUrunler = items,
                    Filtre = filtre,
                    KategoriId = kategoriId
                };

                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = totalCount;
                ViewBag.TotalPages = totalPages;
                ViewBag.HasPrev = page > 1;
                ViewBag.HasNext = page < totalPages;
                ViewBag.Filtre = filtre;
                ViewBag.KategoriId = kategoriId;
                ViewBag.Arama = arama;

                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ürün listesi yüklenirken bir hata oluştu: " + ex.Message;
                ViewBag.Page = 1;
                ViewBag.PageSize = 24;
                ViewBag.TotalCount = 0;
                ViewBag.TotalPages = 0;
                ViewBag.HasPrev = false;
                ViewBag.HasNext = false;
                ViewBag.Filtre = filtre;
                ViewBag.KategoriId = kategoriId;
                ViewBag.Arama = arama;

                return View(new UrunListViewModel { TumUrunler = new List<Urun>(), Filtre = filtre, KategoriId = kategoriId });
            }
        }

        // DETAY
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var urun = await _context.Uruns
                    .AsNoTracking()
                    .Include(u => u.Kategori)
                    .Include(u => u.Yorumlar).ThenInclude(y => y.Kullanici)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (urun == null) return NotFound();

                // pasif mağaza / yayında değil -> gösterme
                var storeAktif = await _context.Magazalar.AnyAsync(m => m.Id == urun.StoreId && m.AktifMi);
                if (!(urun.YayindaMi && urun.Durum == UrunDurum.Yayinda) || !storeAktif)
                {
                    TempData["ErrorMessage"] = "Ürün görüntülenemiyor (mağaza pasif veya ürün yayında değil).";
                    return RedirectToAction(nameof(Index));
                }

                return View(urun);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ürün detayları yüklenirken bir hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // CREATE (GET)
        [HttpGet]
        [Authorize(Roles = "Admin,Satici")]
        public async Task<IActionResult> Create()
        {
            try
            {
                ViewBag.Kategoriler = new SelectList(
                    await _context.Kategoriler.AsNoTracking().OrderBy(x => x.Ad).ToListAsync(), "Id", "Ad");
                return View(new Urun());
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Sayfa yüklenirken bir hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // CREATE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Satici")]
        public async Task<IActionResult> Create(Urun urun, IFormFile? ResimDosyasi, IFormFileCollection? EkResimler)
        {
            try
            {
                // --- Varyasyon doğrulama + normalize ---
                string Normalize(string? s) =>
                    string.Join(',', (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

                if (urun.RenkSecimiVar && string.IsNullOrWhiteSpace(urun.RenkSecenekleri))
                    ModelState.AddModelError(nameof(urun.RenkSecenekleri), "Renk seçeneklerini virgülle yazınız.");
                if (urun.BedenSecimiVar && string.IsNullOrWhiteSpace(urun.BedenSecenekleri))
                    ModelState.AddModelError(nameof(urun.BedenSecenekleri), "Beden seçeneklerini virgülle yazınız.");
                if (urun.BoyutSecimiVar && string.IsNullOrWhiteSpace(urun.BoyutSecenekleri))
                    ModelState.AddModelError(nameof(urun.BoyutSecenekleri), "Boyut seçeneklerini virgülle yazınız.");
                if (urun.NumaraSecimiVar && string.IsNullOrWhiteSpace(urun.NumaraSecenekleri))
                    ModelState.AddModelError(nameof(urun.NumaraSecenekleri), "Numara seçeneklerini virgülle yazınız.");
                if (urun.KapasiteSecimiVar && string.IsNullOrWhiteSpace(urun.KapasiteSecenekleri))
                    ModelState.AddModelError(nameof(urun.KapasiteSecenekleri), "Kapasite seçeneklerini virgülle yazınız.");

                urun.RenkSecenekleri = Normalize(urun.RenkSecenekleri);
                urun.BedenSecenekleri = Normalize(urun.BedenSecenekleri);
                urun.BoyutSecenekleri = Normalize(urun.BoyutSecenekleri);
                urun.NumaraSecenekleri = Normalize(urun.NumaraSecenekleri);
                urun.KapasiteSecenekleri = Normalize(urun.KapasiteSecenekleri);
                urun.MateryalSecenekleri = Normalize(urun.MateryalSecenekleri);
                urun.DesenSecenekleri = Normalize(urun.DesenSecenekleri);

                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                ViewBag.Kategoriler = new SelectList(
                    await _context.Kategoriler.AsNoTracking().OrderBy(x => x.Ad).ToListAsync(), "Id", "Ad", urun.KategoriId);

                // Resim zorunlu
                if (ResimDosyasi == null || ResimDosyasi.Length == 0)
                    ModelState.AddModelError("ResimDosyasi", "Ürün resmi zorunludur.");

                urun.UserId = me.Id;
                urun.EklenmeTarihi = DateTime.Now;

                bool isAdmin = await _userManager.IsInRoleAsync(me, "Admin");

                if (isAdmin)
                {
                    if (!urun.FiyatAdmin.HasValue || urun.FiyatAdmin.Value <= 0)
                        ModelState.AddModelError(nameof(urun.FiyatAdmin), "Admin fiyatı zorunludur.");
                    else
                        urun.Fiyat = urun.FiyatAdmin.Value;

                    urun.Durum = urun.YayindaMi ? UrunDurum.Yayinda : UrunDurum.AdminFiyatBekliyor;
                }
                else // Satici
                {
                    if (!urun.SaticiTeklifFiyati.HasValue || urun.SaticiTeklifFiyati.Value <= 0)
                        ModelState.AddModelError(nameof(urun.SaticiTeklifFiyati), "Satıcı teklif fiyatı zorunludur.");
                    else
                        urun.Fiyat = urun.SaticiTeklifFiyati.Value;

                    urun.Durum = UrunDurum.AdminFiyatBekliyor;
                    urun.YayindaMi = false;
                }

                // Mağaza bağla/oluştur
                var store = await _context.Magazalar.FirstOrDefaultAsync(x => x.OwnerUserId == me.Id);
                if (store == null)
                {
                    store = new Magaza { Ad = $"{me.Ad} {me.Soyad} Mağazası", OwnerUserId = me.Id, AktifMi = true };
                    _context.Magazalar.Add(store);
                    await _context.SaveChangesAsync();
                }
                urun.StoreId = store.Id;

                if (!ModelState.IsValid)
                    return View(urun);

                // Resmi kaydet
                var klasor = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/urunler");
                Directory.CreateDirectory(klasor);
                var anaExt = Path.GetExtension(ResimDosyasi!.FileName);
                var guid = Guid.NewGuid().ToString("N");
                var anaDosya = guid + anaExt;
                using (var fs = System.IO.File.Create(Path.Combine(klasor, anaDosya)))
                    await ResimDosyasi.CopyToAsync(fs);
                urun.ResimUrl = "/images/urunler/" + anaDosya;

                // Ek görselleri kaydet (opsiyonel)
                if (EkResimler != null && EkResimler.Count > 0)
                {
                    int index = 2;
                    foreach (var f in EkResimler.Where(f => f?.Length > 0 && f.ContentType.StartsWith("image/")).Take(6))
                    {
                        var ext = Path.GetExtension(f!.FileName);
                        var adi = $"{guid}_{index}{ext}";
                        using var fsEk = System.IO.File.Create(Path.Combine(klasor, adi));
                        await f.CopyToAsync(fsEk);
                        index++;
                    }
                }
                if (!IsSafeImage(ResimDosyasi))
                {
                    ModelState.AddModelError("ResimDosyasi", "Sadece jpg/png/webp ve max 2MB dosya yükleyin.");
                    return View(urun);
                }

                _context.Uruns.Add(urun);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = isAdmin
                    ? "Ürün eklendi."
                    : "Ürün eklendi, admin fiyatlandırmasından sonra yayınlanacak.";

                return RedirectToAction(nameof(Create));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ürün eklenirken bir hata oluştu: " + ex.Message;
                ViewBag.Kategoriler = new SelectList(_context.Kategoriler.ToList(), "Id", "Ad", urun.KategoriId);
                return View(urun);
            }
        }

        // EDIT (GET)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var urun = await _context.Uruns.FindAsync(id);
                if (urun == null) return NotFound();

                ViewBag.Kategoriler = new SelectList(_context.Kategoriler, "Id", "Ad", urun.KategoriId);
                return View(urun);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Sayfa yüklenirken bir hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, Urun urun, IFormFile? YeniResim, IFormFileCollection? EkResimler)
        {
            string Normalize(string? s) =>
                string.Join(',', (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            if (urun.RenkSecimiVar && string.IsNullOrWhiteSpace(urun.RenkSecenekleri)) ModelState.AddModelError(nameof(urun.RenkSecenekleri), "Renk seçeneklerini virgülle yazınız.");
            if (urun.BedenSecimiVar && string.IsNullOrWhiteSpace(urun.BedenSecenekleri)) ModelState.AddModelError(nameof(urun.BedenSecenekleri), "Beden seçeneklerini virgülle yazınız.");
            if (urun.BoyutSecimiVar && string.IsNullOrWhiteSpace(urun.BoyutSecenekleri)) ModelState.AddModelError(nameof(urun.BoyutSecenekleri), "Boyut seçeneklerini virgülle yazınız.");
            if (urun.NumaraSecimiVar && string.IsNullOrWhiteSpace(urun.NumaraSecenekleri)) ModelState.AddModelError(nameof(urun.NumaraSecenekleri), "Numara seçeneklerini virgülle yazınız.");
            if (urun.KapasiteSecimiVar && string.IsNullOrWhiteSpace(urun.KapasiteSecenekleri)) ModelState.AddModelError(nameof(urun.KapasiteSecenekleri), "Kapasite seçeneklerini virgülle yazınız.");

            urun.RenkSecenekleri = Normalize(urun.RenkSecenekleri);
            urun.BedenSecenekleri = Normalize(urun.BedenSecenekleri);
            urun.BoyutSecenekleri = Normalize(urun.BoyutSecenekleri);
            urun.NumaraSecenekleri = Normalize(urun.NumaraSecenekleri);
            urun.KapasiteSecenekleri = Normalize(urun.KapasiteSecenekleri);
            urun.MateryalSecenekleri = Normalize(urun.MateryalSecenekleri);
            urun.DesenSecenekleri = Normalize(urun.DesenSecenekleri);

            var mevcut = await _context.Uruns.FindAsync(id);
            if (mevcut == null) return NotFound();

            try
            {
                // Temel alanlar
                mevcut.Ad = urun.Ad;
                mevcut.Aciklama = urun.Aciklama;
                mevcut.StokAdedi = urun.StokAdedi;
                mevcut.KategoriId = urun.KategoriId;
                mevcut.IsPopular = urun.IsPopular;
                mevcut.IsSlider = urun.IsSlider;
                mevcut.IsAvantajli = urun.IsAvantajli;
                mevcut.IsCokSatan = urun.IsCokSatan;

                bool Posted(string name) => Request.Form.ContainsKey(name);

                // varyasyon bayrakları
                if (Posted(nameof(urun.RenkSecimiVar))) mevcut.RenkSecimiVar = urun.RenkSecimiVar;
                if (Posted(nameof(urun.BedenSecimiVar))) mevcut.BedenSecimiVar = urun.BedenSecimiVar;
                if (Posted(nameof(urun.BoyutSecimiVar))) mevcut.BoyutSecimiVar = urun.BoyutSecimiVar;
                if (Posted(nameof(urun.NumaraSecimiVar))) mevcut.NumaraSecimiVar = urun.NumaraSecimiVar;
                if (Posted(nameof(urun.KapasiteSecimiVar))) mevcut.KapasiteSecimiVar = urun.KapasiteSecimiVar;

                // varyasyon metinleri
                if (Posted(nameof(urun.RenkSecenekleri))) mevcut.RenkSecenekleri = urun.RenkSecenekleri;
                if (Posted(nameof(urun.BedenSecenekleri))) mevcut.BedenSecenekleri = urun.BedenSecenekleri;
                if (Posted(nameof(urun.BoyutSecenekleri))) mevcut.BoyutSecenekleri = urun.BoyutSecenekleri;
                if (Posted(nameof(urun.NumaraSecenekleri))) mevcut.NumaraSecenekleri = urun.NumaraSecenekleri;
                if (Posted(nameof(urun.KapasiteSecenekleri))) mevcut.KapasiteSecenekleri = urun.KapasiteSecenekleri;
                if (Posted(nameof(urun.MateryalSecenekleri))) mevcut.MateryalSecenekleri = urun.MateryalSecenekleri;
                if (Posted(nameof(urun.DesenSecenekleri))) mevcut.DesenSecenekleri = urun.DesenSecenekleri;

                // --- Satıcı teklif: kültürlü parse & değişim kontrolü ---
                bool teklifAlaniPostlandi = Request.Form.ContainsKey(nameof(urun.SaticiTeklifFiyati));
                string? postedTeklifStr = teklifAlaniPostlandi ? Request.Form[nameof(urun.SaticiTeklifFiyati)].FirstOrDefault() : null;

                bool teklifDegistiMi = false;
                if (teklifAlaniPostlandi && !string.IsNullOrWhiteSpace(postedTeklifStr))
                {
                    var culture = CultureInfo.GetCultureInfo("tr-TR");
                    // 12.50 veya 12,50 kabul
                    if (postedTeklifStr.Contains('.') && !postedTeklifStr.Contains(','))
                        postedTeklifStr = postedTeklifStr.Replace('.', ',');
                    if (decimal.TryParse(postedTeklifStr, NumberStyles.Number, culture, out var postedTeklif))
                    {
                        var eski = mevcut.SaticiTeklifFiyati;
                        decimal eskiVal = eski ?? 0m;

                        if (postedTeklif != 0m && (!eski.HasValue || eskiVal != postedTeklif))
                        {
                            teklifDegistiMi = true;
                            mevcut.SaticiTeklifFiyati = postedTeklif;
                        }
                    }
                }

                if (teklifDegistiMi)
                {
                    mevcut.FiyatAdmin = null;
                    mevcut.FiyatReferansli = null;
                    mevcut.YayindaMi = false;
                    mevcut.Durum = UrunDurum.AdminFiyatBekliyor;
                }

                // --- ADMIN FIYATI (FiyatAdmin) GÜNCELLEME ---
                bool adminFiyatPostlandi = Request.Form.ContainsKey(nameof(urun.FiyatAdmin));
                if (adminFiyatPostlandi)
                {
                    string? postedFiyatStr = Request.Form[nameof(urun.FiyatAdmin)].FirstOrDefault()?.Trim();
                    if (!string.IsNullOrEmpty(postedFiyatStr))
                    {
                        if (postedFiyatStr.Contains('.') && !postedFiyatStr.Contains(','))
                            postedFiyatStr = postedFiyatStr.Replace('.', ',');
                        var tr = CultureInfo.GetCultureInfo("tr-TR");
                        if (decimal.TryParse(postedFiyatStr, NumberStyles.Number, tr, out var postedFiyat) && postedFiyat > 0)
                        {
                            mevcut.FiyatAdmin = postedFiyat;
                            mevcut.Fiyat = postedFiyat; // vitrindeki fiyat
                        }
                        else
                        {
                            ModelState.AddModelError(nameof(urun.FiyatAdmin), "Admin fiyatı geçerli bir sayı olmalıdır.");
                        }
                    }
                    else
                    {
                        // boş bırakılırsa admin fiyatını temizlemek isteniyorsa:
                        mevcut.FiyatAdmin = null;
                        // Fiyat alanını koruyoruz; yayın durumu aşağıda belirlenir.
                    }
                }

                // --- YayindaMi bayrağı postlandıysa güncelle ---
                if (Posted(nameof(urun.YayindaMi)))
                    mevcut.YayindaMi = urun.YayindaMi;

                // --- Durum senkronizasyonu ---
                if (mevcut.FiyatAdmin.HasValue && mevcut.FiyatAdmin.Value > 0)
                {
                    mevcut.Durum = mevcut.YayindaMi ? UrunDurum.Yayinda : UrunDurum.AdminFiyatBekliyor;
                }
                else
                {
                    mevcut.Durum = UrunDurum.AdminFiyatBekliyor;
                    mevcut.YayindaMi = false; // admin fiyat yokken yayına çıkma
                }

                // resim işlemi (ana)
                string? guid = null; // ana dosyanın isim kökü
                if (YeniResim != null && YeniResim.Length > 0)
                {
                    // Eski ana görseli sil
                    if (!string.IsNullOrEmpty(mevcut.ResimUrl))
                    {
                        var eskiYol = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", mevcut.ResimUrl.TrimStart('/'));
                        if (System.IO.File.Exists(eskiYol)) System.IO.File.Delete(eskiYol);
                    }

                    var klasor = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/urunler");
                    Directory.CreateDirectory(klasor);

                    var anaExt = Path.GetExtension(YeniResim.FileName);
                    guid = Guid.NewGuid().ToString("N");
                    var dosya = guid + anaExt;
                    using (var fs = System.IO.File.Create(Path.Combine(klasor, dosya)))
                        await YeniResim.CopyToAsync(fs);

                    mevcut.ResimUrl = "/images/urunler/" + dosya;
                }

                // Ek görselleri ekle (opsiyonel)
                if (EkResimler != null && EkResimler.Count > 0)
                {
                    var klasor = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/urunler");
                    Directory.CreateDirectory(klasor);

                    // Eğer yeni ana görsel gelmediyse mevcut ana görselin kökünü kullan
                    if (string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(mevcut.ResimUrl))
                    {
                        var anaAd = Path.GetFileNameWithoutExtension(mevcut.ResimUrl);
                        guid = anaAd; // mevcut kök
                    }

                    // Kökü düzelt ve sıradaki index'i bul
                    if (!string.IsNullOrEmpty(guid))
                    {
                        int nextIndex = 2;
                        var anaRel = (mevcut.ResimUrl ?? string.Empty).TrimStart('/');
                        var fizAna = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", anaRel);
                        var dir = Path.GetDirectoryName(fizAna) ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "urunler");
                        var stem = guid!;

                        if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                        {
                            var mevcutEkler = System.IO.Directory.GetFiles(dir)
                                .Select(p => Path.GetFileNameWithoutExtension(p))
                                .Where(name =>
                                    name.StartsWith(stem + "_") ||
                                    name.StartsWith(stem + "-") ||
                                    name.StartsWith(stem + "."))
                                .Select(name =>
                                {
                                    var tail = name.Substring(stem.Length + 1);
                                    return int.TryParse(tail, out var n) ? n : (int?)null;
                                })
                                .Where(n => n.HasValue)
                                .Select(n => n!.Value)
                                .ToList();

                            if (mevcutEkler.Any())
                                nextIndex = mevcutEkler.Max() + 1;
                        }

                        foreach (var f in EkResimler.Where(f => f?.Length > 0 && f.ContentType.StartsWith("image/")).Take(6))
                        {
                            var ext = Path.GetExtension(f!.FileName);
                            var adi = $"{stem}_{nextIndex}{ext}";
                            using var fsEk = System.IO.File.Create(Path.Combine(klasor, adi));
                            await f.CopyToAsync(fsEk);
                            nextIndex++;
                        }
                    }
                }
                if (YeniResim != null && !IsSafeImage(YeniResim))
                {
                    ModelState.AddModelError("YeniResim", "Sadece jpg/png/webp ve max 2MB dosya yükleyin.");
                    ViewBag.Kategoriler = new SelectList(_context.Kategoriler.ToList(), "Id", "Ad", mevcut.KategoriId);
                    return View(mevcut);
                }

                _context.Update(mevcut);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Ürün başarıyla güncellendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Bir hata oluştu: " + ex.Message;
                ViewBag.Kategoriler = new SelectList(_context.Kategoriler.ToList(), "Id", "Ad", mevcut.KategoriId);
                return View(mevcut);
            }
        }

        // DELETE (GET)
        [HttpGet]
        [Authorize(Roles = "Admin,Satici")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var urun = await _context.Uruns
                    .AsNoTracking()
                    .Include(u => u.Kategori)
                    .FirstOrDefaultAsync(u => u.Id == id);
                if (urun == null) return NotFound();

                return View(urun);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Sayfa yüklenirken bir hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // DELETE (POST) - Confirmed
        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Satici")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var urun = await _context.Uruns.FindAsync(id);
                if (urun == null) return NotFound();

                // Ana + ek görselleri sil
                if (!string.IsNullOrWhiteSpace(urun.ResimUrl))
                {
                    var anaFiziksel = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", urun.ResimUrl.TrimStart('/'));
                    var dir = Path.GetDirectoryName(anaFiziksel);
                    var stem = Path.GetFileNameWithoutExtension(anaFiziksel);

                    // Ana
                    if (System.IO.File.Exists(anaFiziksel)) System.IO.File.Delete(anaFiziksel);

                    // Ekler
                    if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                    {
                        foreach (var p in System.IO.Directory.GetFiles(dir))
                        {
                            var name = Path.GetFileNameWithoutExtension(p);
                            if (name.Equals(Path.GetFileNameWithoutExtension(anaFiziksel), StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (name.StartsWith(stem + "_") || name.StartsWith(stem + "-") || name.StartsWith(stem + "."))
                            {
                                try { System.IO.File.Delete(p); } catch { /* yut */ }
                            }
                        }
                    }
                }

                _context.Uruns.Remove(urun);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Ürün silindi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ürün silinirken bir hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // Eski Delete (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Satici")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var urun = await _context.Uruns.FindAsync(id);
                if (urun == null) return NotFound();

                // Ana + ek görselleri sil
                if (!string.IsNullOrWhiteSpace(urun.ResimUrl))
                {
                    var anaFiziksel = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", urun.ResimUrl.TrimStart('/'));
                    var dir = Path.GetDirectoryName(anaFiziksel);
                    var stem = Path.GetFileNameWithoutExtension(anaFiziksel);

                    if (System.IO.File.Exists(anaFiziksel)) System.IO.File.Delete(anaFiziksel);

                    if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                    {
                        foreach (var p in System.IO.Directory.GetFiles(dir))
                        {
                            var name = Path.GetFileNameWithoutExtension(p);
                            if (name.Equals(Path.GetFileNameWithoutExtension(anaFiziksel), StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (name.StartsWith(stem + "_") || name.StartsWith(stem + "-") || name.StartsWith(stem + "."))
                            {
                                try { System.IO.File.Delete(p); } catch { }
                            }
                        }
                    }
                }

                _context.Uruns.Remove(urun);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Ürün silindi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ürün silinirken bir hata oluştu: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // HEMEN SATIN AL (ÖDEME ÖNCESİ: sipariş/bonus/harcama/stock YOK)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CanPurchase")]
        public async Task<IActionResult> BuyNow(
            int id,
            string? renk = null, string? beden = null, string? boyut = null,
            string? numara = null, string? kapasite = null, string? materyal = null, string? desen = null)
        {
            var urun = await _context.Uruns.FindAsync(id);
            if (urun == null) return NotFound();

            // mağaza aktif mi?
            var storeAktif = await _context.Magazalar.AnyAsync(m => m.Id == urun.StoreId && m.AktifMi);
            if (!storeAktif) { TempData["ErrorMessage"] = "Bu ürünün mağazası pasif. Satın alma yapılamaz."; return RedirectToAction("Details", new { id }); }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // admin/satıcı satın alamaz
            if (await _userManager.IsInRoleAsync(user, "Admin") || await _userManager.IsInRoleAsync(user, "Satici"))
            { TempData["ErrorMessage"] = "Admin veya Satıcılar ürün satın alamaz."; return RedirectToAction("Details", new { id }); }

            // zorunlu varyasyonlar
            if (urun.RenkSecimiVar && string.IsNullOrEmpty(renk)) { TempData["ErrorMessage"] = "Lütfen renk seçiniz."; return RedirectToAction("Details", new { id }); }
            if (urun.BedenSecimiVar && string.IsNullOrEmpty(beden)) { TempData["ErrorMessage"] = "Lütfen beden seçiniz."; return RedirectToAction("Details", new { id }); }
            if (urun.BoyutSecimiVar && string.IsNullOrEmpty(boyut)) { TempData["ErrorMessage"] = "Lütfen boyut seçiniz."; return RedirectToAction("Details", new { id }); }
            if (urun.NumaraSecimiVar && string.IsNullOrEmpty(numara)) { TempData["ErrorMessage"] = "Lütfen numara seçiniz."; return RedirectToAction("Details", new { id }); }
            if (urun.KapasiteSecimiVar && string.IsNullOrEmpty(kapasite)) { TempData["ErrorMessage"] = "Lütfen kapasite seçiniz."; return RedirectToAction("Details", new { id }); }
            if (urun.StokAdedi < 1) { TempData["ErrorMessage"] = "Stokta yeterli ürün yok."; return RedirectToAction("Details", new { id }); }

            // adres zorunluluğu
            var varsayilanAdres = await _context.Adresler
                .Where(a => a.KullaniciId == user.Id)
                .OrderByDescending(a => a.IsVarsayilan)
                .FirstOrDefaultAsync();
            if (varsayilanAdres == null)
            {
                TempData["ErrorMessage"] = "Satın almak için lütfen önce bir adres ekleyin.";
                return RedirectToAction("Create", "Adresler", new { returnUrl = Url.Action("Details", "Uruns", new { id }) });
            }

            // SEPETİ tek satıra düşür (BuyNow)
            var mevcutSepet = await _context.Sepet.Where(s => s.KullaniciId == user.Id).ToListAsync();
            if (mevcutSepet.Any())
                _context.Sepet.RemoveRange(mevcutSepet);

            _context.Sepet.Add(new SepetKalemi
            {
                KullaniciId = user.Id,
                UrunId = urun.Id,
                Miktar = 1,
                Renk = renk,
                Beden = beden,
                Boyut = boyut,
                Numara = numara,
                Kapasite = kapasite,
                Materyal = materyal,
                Desen = desen,
                Aciklama = $"Renk:{renk ?? "-"} | Beden:{beden ?? "-"} | Boyut:{boyut ?? "-"} | Numara:{numara ?? "-"} | Kapasite:{kapasite ?? "-"} | Materyal:{materyal ?? "-"} | Desen:{desen ?? "-"}"
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Ödeme adımına geçiliyor…";
            return RedirectToAction("Baslat", "Odeme");
        }

    }
}
