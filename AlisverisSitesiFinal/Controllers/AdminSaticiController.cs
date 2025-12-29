using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Transactions;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminSaticiController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminSaticiController(
            UygulamaDbContext context,
            UserManager<Kullanici> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // =============== LISTE ===============
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var list = await _context.Magazalar
                .OrderByDescending(m => m.OlusturmaTarihi)
                .Select(m => new SaticiListItemVM
                {
                    MagazaId = m.Id,
                    MagazaAdi = m.Ad,
                    OwnerUserId = m.OwnerUserId,
                    AktifMi = m.AktifMi,
                    OlusturmaTarihi = m.OlusturmaTarihi,
                    VergiNo = m.VergiNo,
                    IBAN = m.IBAN,
                    Telefon = m.Telefon,
                    Adres = m.Adres
                })
                .ToListAsync();

            // Kullanıcı bilgileri + rol
            var userIds = list.Select(i => i.OwnerUserId).Distinct().ToList();
            var users = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Ad, u.Soyad, u.Email })
                .ToListAsync();

            foreach (var it in list)
            {
                var u = users.FirstOrDefault(x => x.Id == it.OwnerUserId);
                if (u != null)
                {
                    it.SaticiAdSoyad = $"{u.Ad} {u.Soyad}".Trim();
                    it.Email = u.Email ?? "";
                }
            }

            foreach (var it in list)
            {
                var user = await _userManager.FindByIdAsync(it.OwnerUserId);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    it.SaticiRolu = roles.Contains("Satici") ? "Satici"
                                : roles.Contains("SaticiAday") ? "SaticiAday"
                                : "(rol yok)";
                }
            }

            // Ürün özetleri
            var magazaIds = list.Select(x => x.MagazaId).ToList();

            var urunGruplari = await _context.Uruns
                .Include(u => u.Kategori)
                .Where(u => u.StoreId.HasValue && magazaIds.Contains(u.StoreId.Value))
                .OrderByDescending(u => u.EklenmeTarihi)
                .Select(u => new
                {
                    StoreId = u.StoreId.GetValueOrDefault(),
                    VM = new AdminUrunListeVM
                    {
                        UrunId = u.Id,
                        UrunAdi = u.Ad,
                        Kategori = u.Kategori != null ? u.Kategori.Ad : null,
                        Stok = u.StokAdedi,
                        EklenmeTarihi = u.EklenmeTarihi,
                        YayindaMi = u.YayindaMi,
                        SaticiId = u.UserId ?? "",
                        SaticiAdSoyad = "",
                        SaticiEmail = "",
                        SaticiTeklifFiyati = u.SaticiTeklifFiyati,
                        FiyatAdmin = u.FiyatAdmin,
                        FiyatReferansli = u.FiyatReferansli
                    }
                })
                .ToListAsync();

            ViewBag.UrunlerByMagaza = urunGruplari
                .GroupBy(x => x.StoreId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.VM).ToList());

            return View(list);
        }

        // =============== MAĞAZA AKTİF/PASİF ===============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStore(int id)
        {
            var magaza = await _context.Magazalar.FindAsync(id);
            if (magaza == null)
            {
                TempData["Error"] = "Mağaza bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            magaza.AktifMi = !magaza.AktifMi;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Mağaza {(magaza.AktifMi ? "AKTİF" : "PASİF")} yapıldı.";
            return RedirectToAction(nameof(Index));
        }

        // =============== SATICI ROLÜ (Satici ⇄ SaticiAday) ===============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSellerRole(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            // Kullanıcının mağazası
            var store = await _context.Magazalar.FirstOrDefaultAsync(m => m.OwnerUserId == user.Id);
            var roles = await _userManager.GetRolesAsync(user);
            bool isSatici = roles.Contains("Satici");
            bool isAday = roles.Contains("SaticiAday");

            // Roller garanti
            if (!await _roleManager.RoleExistsAsync("Satici"))
                await _roleManager.CreateAsync(new IdentityRole("Satici"));
            if (!await _roleManager.RoleExistsAsync("SaticiAday"))
                await _roleManager.CreateAsync(new IdentityRole("SaticiAday"));

            IdentityResult res;

            if (isSatici)
            {
                if (store != null && store.AktifMi)
                {
                    TempData["Error"] = "Mağazası aktif olan bir satıcı ADAY yapılamaz. Önce mağazayı pasif yapın.";
                    return RedirectToAction(nameof(Index));
                }

                await _userManager.RemoveFromRoleAsync(user, "Satici");
                res = await _userManager.AddToRoleAsync(user, "SaticiAday");
                TempData["Success"] = "Satıcı rolü ADAY’a çevrildi.";
            }
            else
            {
                if (isAday) await _userManager.RemoveFromRoleAsync(user, "SaticiAday");
                res = await _userManager.AddToRoleAsync(user, "Satici");
                TempData["Success"] = "Kullanıcıya SATICI rolü verildi.";
            }

            if (!res.Succeeded)
                TempData["Error"] = string.Join(" | ", res.Errors.Select(e => e.Description));

            await _userManager.UpdateSecurityStampAsync(user);
            return RedirectToAction(nameof(Index));
        }

        // =============== yardımcılar ===============
        private static string? Digits(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : new string(s.Where(char.IsDigit).ToArray());

        private static string? Limit(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private async Task<string> EnsureUniqueStoreNameAsync(string baseName)
        {
            var name = Limit(baseName, 200) ?? "Magaza";

            bool exists = await _context.Magazalar.AsNoTracking()
                .AnyAsync(x => x.Ad != null && x.Ad.Trim().ToUpper() == name.ToUpper());
            if (!exists) return name;

            for (int i = 2; i < 5000; i++)
            {
                var cand = Limit($"{name} ({i})", 200)!;
                bool clash = await _context.Magazalar.AsNoTracking()
                    .AnyAsync(x => x.Ad != null && x.Ad.Trim().ToUpper() == cand.ToUpper());
                if (!clash) return cand;
            }
            return $"{name} - {DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        private async Task<string?> EnsureUniqueVergiNoAsync(string? vergiNo)
        {
            if (string.IsNullOrWhiteSpace(vergiNo)) return null;
            var digits = Digits(vergiNo);
            if (digits is not { Length: 10 } and not { Length: 11 }) return digits;

            string candidate = digits!;
            int tries = 0;
            while (await _context.Magazalar.AsNoTracking().AnyAsync(x => x.VergiNo == candidate))
            {
                tries++;
                var rnd = new Random().Next(0, (int)Math.Pow(10, candidate.Length) - 1);
                candidate = rnd.ToString().PadLeft(candidate.Length, '0');
                if (tries > 20) break;
            }
            return candidate;
        }

        private async Task<string?> EnsureUniqueIbanAsync(string? iban)
        {
            if (string.IsNullOrWhiteSpace(iban)) return null;
            var baseIban = Limit(iban.Replace(" ", "").ToUpperInvariant(), 34)!;

            string candidate = baseIban;
            int suffix = 2;
            while (await _context.Magazalar.AsNoTracking().AnyAsync(x => x.IBAN == candidate))
            {
                var tail = $"-{suffix:00}";
                candidate = Limit(baseIban + tail, 34)!;
                suffix++;
                if (suffix > 99) break;
            }
            return candidate;
        }

        // >>>>>>>>>>> SQL hata detayını teşhis eden ufak yardımcılar
        private static (string msg, int? code) BuildSqlUserMessage(SqlException sql, string? idx, string? col, string? reqId)
        {
            string baseMsg = sql.Number switch
            {
                2601 or 2627 => "Tekrarlı kayıt (benzersiz indeks ihlali)",
                2628 or 8152 => "Bir metin alanı kolon uzunluğunu aşıyor",
                515 => "Zorunlu bir alan boş bırakılmış",
                547 => "İlişkili kayıt kısıtlaması (FK) ihlali",
                245 => "Veri türü dönüşüm hatası",
                _ => "Veritabanı hatası"
            };

            var bits = baseMsg + $" (SQL {sql.Number}"
                      + (string.IsNullOrWhiteSpace(idx) ? "" : $", index: {idx}")
                      + (string.IsNullOrWhiteSpace(col) ? "" : $", alan: {col}")
                      + ")";

            if (!string.IsNullOrWhiteSpace(reqId))
                bits += $" (İstek No: {reqId})";

            return (bits, sql.Number);
        }

        private static (string? indexName, string? columnName) TryParseSqlDetails(SqlException sql)
        {
            string? idx = null, col = null;

            // index/constraint adı
            var mIdx = Regex.Match(sql.Message,
                @"(?:index|constraint|dizin|kısıtlama)\s+'([^']+)'",
                RegexOptions.IgnoreCase);
            if (mIdx.Success) idx = mIdx.Groups[1].Value;

            // kolon adı (SQL 2019+)
            var mCol = Regex.Match(sql.Message,
                @"column '([^']+)'",
                RegexOptions.IgnoreCase);
            if (mCol.Success) col = mCol.Groups[1].Value;

            return (idx, col);
        }
        // <<<<<<<<<<<< yardımcılar sonu

        // =============== CREATE (GET) ===============
        [HttpGet]
        public IActionResult Create()
        {
            return View(new AdminSaticiVM { OnayliSatici = true, AktifMi = true });
        }

        // =============== CREATE (POST) ===============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminSaticiVM m)
        {
            if (!ModelState.IsValid) return View(m);

            var reqId = HttpContext?.TraceIdentifier;

            try
            {
                // 1) Kullanıcıyı bul/oluştur (Email + UserName normalize)
                var email = Limit(m.Email, 256);
                if (string.IsNullOrWhiteSpace(email))
                {
                    ModelState.AddModelError(nameof(m.Email), "E-posta gerekli.");
                    return View(m);
                }

                var norm = _userManager.NormalizeEmail(email);
                var user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.NormalizedEmail == norm || u.NormalizedUserName == norm);

                bool createdNewUser = false;
                if (user == null)
                {
                    var pwd = string.IsNullOrWhiteSpace(m.Sifre) ? $"Aa!{Guid.NewGuid():N}a1" : m.Sifre;
                    user = new Kullanici
                    {
                        UserName = email,
                        Email = email,
                        Ad = Limit(m.Ad, 100),
                        Soyad = Limit(m.Soyad, 100),
                        EmailConfirmed = true
                    };
                    var cres = await _userManager.CreateAsync(user, pwd);
                    if (!cres.Succeeded)
                    {
                        foreach (var e in cres.Errors) ModelState.AddModelError("", e.Description);
                        return View(m);
                    }
                    createdNewUser = true;
                }
                else
                {
                    var updated = false;
                    if (string.IsNullOrWhiteSpace(user.Ad) && !string.IsNullOrWhiteSpace(m.Ad)) { user.Ad = Limit(m.Ad, 100); updated = true; }
                    if (string.IsNullOrWhiteSpace(user.Soyad) && !string.IsNullOrWhiteSpace(m.Soyad)) { user.Soyad = Limit(m.Soyad, 100); updated = true; }
                    if (updated) await _userManager.UpdateAsync(user);
                }

                // 2) Roller ve atama
                if (!await _roleManager.RoleExistsAsync("Satici"))
                    await _roleManager.CreateAsync(new IdentityRole("Satici"));
                if (!await _roleManager.RoleExistsAsync("SaticiAday"))
                    await _roleManager.CreateAsync(new IdentityRole("SaticiAday"));

                var hedefRol = m.OnayliSatici ? "Satici" : "SaticiAday";
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains(hedefRol))
                {
                    foreach (var r in roles) await _userManager.RemoveFromRoleAsync(user, r);
                    var roleRes = await _userManager.AddToRoleAsync(user, hedefRol);
                    if (!roleRes.Succeeded)
                    {
                        foreach (var e in roleRes.Errors) ModelState.AddModelError("", e.Description);
                        return View(m);
                    }
                }

                // 3) Alanları normalize/kısıtla
                var vergiNo = Digits(m.VergiNo);
                if (!string.IsNullOrEmpty(vergiNo) && !(vergiNo.Length == 10 || vergiNo.Length == 11))
                {
                    ModelState.AddModelError(nameof(m.VergiNo), "Vergi/T.C. numarası 10 veya 11 haneli olmalıdır.");
                    return View(m);
                }
                var iban = Limit(string.IsNullOrWhiteSpace(m.IBAN) ? null : m.IBAN.Replace(" ", "").ToUpperInvariant(), 34);
                var hedefAd = await EnsureUniqueStoreNameAsync(
                    string.IsNullOrWhiteSpace(m.MagazaAdi)
                        ? $"{Limit(m.Ad, 100)} {Limit(m.Soyad, 100)} Mağazası".Trim()
                        : m.MagazaAdi.Trim()
                );

                // 4) Mağaza: varsa güncelle, yoksa oluştur
                var store = await _context.Magazalar.FirstOrDefaultAsync(x => x.OwnerUserId == user.Id);
                if (store == null)
                {
                    store = new Magaza
                    {
                        Ad = hedefAd,
                        OwnerUserId = user.Id,
                        VergiNo = vergiNo,
                        IBAN = iban,
                        Telefon = Limit(m.Telefon, 32),
                        Adres = Limit(m.Adres, 500),
                        AktifMi = m.AktifMi,
                        OlusturmaTarihi = DateTime.UtcNow
                    };
                    _context.Magazalar.Add(store);
                }
                else
                {
                    store.Ad = hedefAd;
                    store.VergiNo = vergiNo;
                    store.IBAN = iban;
                    store.Telefon = Limit(m.Telefon, 32);
                    store.Adres = Limit(m.Adres, 500);
                    store.AktifMi = m.AktifMi;
                }

                // 5) Kaydet – tekillik/uzunluk hatalarında otomatik düzeltme + tekrar dene
                SqlException? lastDupSql = null;

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        await _context.SaveChangesAsync();
                        TempData["Success"] =
                            $"{(createdNewUser ? "Kullanıcı oluşturuldu ve " : "Mevcut kullanıcı kullanıldı; ")}" +
                            $"'{hedefRol}' rolü atandı. Mağaza {(m.AktifMi ? "AKTİF" : "PASİF")} kaydedildi.";
                        ModelState.Clear();
                        return View(new AdminSaticiVM { OnayliSatici = true, AktifMi = true });
                    }
                    catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                    {
                        // duplicate: index adını ayrıştır
                        lastDupSql = sql;
                        var match = Regex.Match(sql.Message, @"(?:index|constraint|dizin|kısıtlama)\s+'([^']+)'",
                                                RegexOptions.IgnoreCase);
                        var idx = match.Success ? match.Groups[1].Value.ToLowerInvariant() : "";

                        if (idx.Contains("ad"))
                        {
                            store.Ad = await EnsureUniqueStoreNameAsync(store.Ad + $" - {DateTime.UtcNow:HHmmssfff}");
                            continue;
                        }
                        if (idx.Contains("vergino"))
                        {
                            store.VergiNo = await EnsureUniqueVergiNoAsync(store.VergiNo);
                            continue;
                        }
                        if (idx.Contains("iban"))
                        {
                            store.IBAN = await EnsureUniqueIbanAsync(store.IBAN);
                            continue;
                        }
                        if (idx.Contains("owneruserid") || idx.Contains("owner_user_id"))
                        {
                            var existed = await _context.Magazalar.FirstOrDefaultAsync(x => x.OwnerUserId == user.Id);
                            if (existed != null)
                            {
                                existed.Ad = await EnsureUniqueStoreNameAsync(existed.Ad + $" - {DateTime.UtcNow:HHmmssfff}");
                                _context.Entry(store).State = EntityState.Detached;
                                store = existed;
                                continue;
                            }
                        }

                        // bilinmiyorsa: mağaza adını benzersizleştir
                        store.Ad = await EnsureUniqueStoreNameAsync(store.Ad + $" - {DateTime.UtcNow:HHmmssfff}");
                        continue;
                    }
                }

                // 3 denemeden sonra hâlâ duplicate ise ayrıntıyı göster
                if (lastDupSql != null)
                {
                    var (idxName, colName) = TryParseSqlDetails(lastDupSql);
                    var (msg, _) = BuildSqlUserMessage(lastDupSql, idxName, colName, reqId);
                    ModelState.AddModelError("", msg);
                }
                else
                {
                    ModelState.AddModelError("", "Tekrarlı kayıt hatası oluştu; otomatik düzeltme başarısız.");
                }
                return View(m);
            }
            catch (DbUpdateException ex)
            {
                string userMsg = "Kayıt sırasında veritabanı hatası oluştu";
                if (ex.InnerException is SqlException sql)
                {
                    var (idxName, colName) = TryParseSqlDetails(sql);
                    var (msg, _) = BuildSqlUserMessage(sql, idxName, colName, reqId);
                    userMsg = msg;
                }

                ModelState.AddModelError("", userMsg);
                return View(m);
            }
            catch (Exception)
            {
                ModelState.AddModelError("", $"Beklenmeyen bir hata oluştu. (İstek No: {reqId})");
                return View(m);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteStore(int id, bool lockAccount = true)
        {
            try
            {
                // 0) Mağaza
                var magaza = await _context.Magazalar
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (magaza == null)
                {
                    TempData["Error"] = "Mağaza bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                // 0.1) Mağaza sahibini getir (Identity)
                Kullanici? satici = null;
                if (!string.IsNullOrWhiteSpace(magaza.OwnerUserId))
                    satici = await _userManager.FindByIdAsync(magaza.OwnerUserId);

                // 1) Satıcı rol/hesap işlemleri (EF transaction dışında)
                if (satici != null)
                {
                    var roles = await _userManager.GetRolesAsync(satici);

                    if (roles.Contains("Satici"))
                        await _userManager.RemoveFromRoleAsync(satici, "Satici");

                    // İsteğe bağlı temel rol adı
                    const string baseRole = "User";

                    // (A) Rol varsa ekle, yoksa atla (hatasız)
                    if (await _roleManager.RoleExistsAsync(baseRole))
                    {
                        if (!roles.Contains(baseRole))
                            await _userManager.AddToRoleAsync(satici, baseRole);
                    }
                    // (B) Rol otomatik oluşturulsun istersen üstteki if yerine şunu kullan:
                    // if (!await _roleManager.RoleExistsAsync(baseRole))
                    //     await _roleManager.CreateAsync(new IdentityRole(baseRole));
                    // if (!roles.Contains(baseRole))
                    //     await _userManager.AddToRoleAsync(satici, baseRole);

                    if (lockAccount)
                    {
                        satici.LockoutEnabled = true;
                        satici.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                        await _userManager.UpdateAsync(satici);
                    }
                }

                // 2) Mağaza/Ürün/Sipariş kalemi silme — tek DB transaction
                await using (var tx = await _context.Database.BeginTransactionAsync())
                {
                    var urunIdler = await _context.Uruns
                        .Where(u => u.StoreId == magaza.Id)
                        .Select(u => u.Id)
                        .ToListAsync();

                    if (urunIdler.Count > 0)
                    {
                        var kalemler = await _context.SiparisKalemleri
                            .Where(sk => urunIdler.Contains(sk.UrunId))
                            .ToListAsync();
                        if (kalemler.Count > 0)
                            _context.SiparisKalemleri.RemoveRange(kalemler);

                        var urunEntities = await _context.Uruns
                            .Where(u => urunIdler.Contains(u.Id))
                            .ToListAsync();
                        if (urunEntities.Count > 0)
                            _context.Uruns.RemoveRange(urunEntities);
                    }

                    var stub = new Magaza { Id = magaza.Id };
                    _context.Attach(stub);
                    _context.Remove(stub);

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                }

                TempData["Success"] = "Mağaza ve bağlı ürünler silindi. Satıcı yetkisi kaldırıldı" + (lockAccount ? " ve hesap kilitlendi." : ".");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Mağaza silinirken bir hata oluştu: " + (ex.InnerException?.Message ?? ex.Message);
                return RedirectToAction(nameof(Index));
            }
        }


    }
}
