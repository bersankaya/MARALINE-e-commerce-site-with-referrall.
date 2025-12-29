using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class KategorisController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly IWebHostEnvironment _env;

        public KategorisController(UygulamaDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var kategoriler = await _context.Kategoriler.OrderBy(k => k.Ad).ToListAsync();
            return View(kategoriler);
        }

        // GET: Kategoriler/Create
        public IActionResult Create()
        {
            return View(new Kategori()); // boş model gönderiyoruz
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Kategori kategori)
        {
            if (ModelState.IsValid)
            {
                bool varMi = await _context.Kategoriler.AnyAsync(k => k.Ad.ToLower() == kategori.Ad.ToLower());
                if (varMi)
                {
                    ModelState.AddModelError("Ad", "Bu kategori zaten mevcut.");
                    return View(kategori);
                }

                // Dosya yüklemesi Request.Form.Files üzerinden alınıyor (metod imzası bozulmadı)
                var file = Request.Form.Files.FirstOrDefault(); // <input name="ImageFile" />
                if (file != null && file.Length > 0)
                {
                    // Basit validasyon
                    var allowedExt = new[] { ".jpg", ".jpeg", ".png" };
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExt.Contains(ext))
                    {
                        ModelState.AddModelError("ImageFile", "Sadece JPG/PNG formatı desteklenir.");
                        return View(kategori);
                    }

                    const long maxBytes = 2 * 1024 * 1024; // 2 MB
                    if (file.Length > maxBytes)
                    {
                        ModelState.AddModelError("ImageFile", "Dosya boyutu maksimum 2 MB olabilir.");
                        return View(kategori);
                    }

                    var uploadDir = Path.Combine(_env.WebRootPath, "images", "kategoriler");
                    if (!Directory.Exists(uploadDir))
                        Directory.CreateDirectory(uploadDir);

                    var fileName = Guid.NewGuid().ToString("N") + ext;
                    var filePath = Path.Combine(uploadDir, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Veritabanına kaydedilecek url
                    kategori.ImageUrl = $"/images/kategoriler/{fileName}";
                }

                _context.Add(kategori);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(kategori);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var kategori = await _context.Kategoriler.FindAsync(id);
            if (kategori == null) return NotFound();

            return View(kategori);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Kategori kategori)
        {
            if (id != kategori.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Mevcut kaydı DB'den çekiyoruz (diğer alanları korumak ve concurrency kontrolü için)
                    var mevcut = await _context.Kategoriler.FindAsync(id);
                    if (mevcut == null) return NotFound();

                    // Aynı isimde farklı bir kategori var mı kontrolü
                    bool varMi = await _context.Kategoriler
                        .AnyAsync(k => k.Id != id && k.Ad.ToLower() == kategori.Ad.ToLower());
                    if (varMi)
                    {
                        ModelState.AddModelError("Ad", "Bu kategori adı başka bir kayıt tarafından kullanılıyor.");
                        return View(kategori);
                    }

                    // Dosya yüklemesi varsa al
                    var file = Request.Form.Files.FirstOrDefault();
                    if (file != null && file.Length > 0)
                    {
                        var allowedExt = new[] { ".jpg", ".jpeg", ".png" };
                        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!allowedExt.Contains(ext))
                        {
                            ModelState.AddModelError("ImageFile", "Sadece JPG/PNG formatı desteklenir.");
                            return View(kategori);
                        }

                        const long maxBytes = 2 * 1024 * 1024; // 2 MB
                        if (file.Length > maxBytes)
                        {
                            ModelState.AddModelError("ImageFile", "Dosya boyutu maksimum 2 MB olabilir.");
                            return View(kategori);
                        }

                        var uploadDir = Path.Combine(_env.WebRootPath, "images", "kategoriler");
                        if (!Directory.Exists(uploadDir))
                            Directory.CreateDirectory(uploadDir);

                        var fileName = Guid.NewGuid().ToString("N") + ext;
                        var filePath = Path.Combine(uploadDir, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Eski görsel varsa sil (isteğe bağlı)
                        if (!string.IsNullOrEmpty(mevcut.ImageUrl))
                        {
                            try
                            {
                                var eskiPath = Path.Combine(_env.WebRootPath, mevcut.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                                if (System.IO.File.Exists(eskiPath))
                                    System.IO.File.Delete(eskiPath);
                            }
                            catch
                            {
                                // Silmede hata olursa yoksay (log ekleyebilirsin)
                            }
                        }

                        mevcut.ImageUrl = $"/images/kategoriler/{fileName}";
                    }

                    // Güncellenecek alanlar
                    mevcut.Ad = kategori.Ad!.Trim();

                    // Diğer alanlar varsa burada tek tek ata (ör. açıklama vs.)
                    _context.Update(mevcut);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Kategoriler.AnyAsync(k => k.Id == id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(kategori);
        }


        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var kategori = await _context.Kategoriler.FindAsync(id);
            if (kategori == null) return NotFound();

            return View(kategori);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var kategori = await _context.Kategoriler.FindAsync(id);
            if (kategori != null)
            {
                _context.Kategoriler.Remove(kategori);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
