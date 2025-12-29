using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Satici,Admin")]
    public class SaticiSiparisleriController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;
        private readonly IOptions<ReferralConfig> _config;
        private readonly PayTRService _paytr;

        public SaticiSiparisleriController(UygulamaDbContext ctx, UserManager<Kullanici> um, IOptions<ReferralConfig> config, PayTRService paytr)
        {
            _context = ctx; _userManager = um; _config = config; _paytr = paytr;
        }

        private static bool CanApprove(string? d) => d == "Beklemede";
        private static bool CanReject(string? d) => d == "Beklemede";
        private static bool CanShip(string? d) => d == "Onaylandı";
        private static bool CanComplete(string? d) => d == "Kargolandı";

        public async Task<IActionResult> Index()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var myStoreId = await _context.Magazalar
                .Where(x => x.OwnerUserId == me.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            var kalemler = await _context.SiparisKalemleri
                .Include(k => k.Siparis).ThenInclude(s => s!.Adres)
                .Include(k => k.Urun)
                .Where(k => k.Urun != null &&
                            (k.SaticiId == me.Id || k.Urun.StoreId == myStoreId))
                .OrderByDescending(k => k.Siparis!.SiparisTarihi)
                .ToListAsync();

            var musteriIdSet = kalemler
                .Where(k => k.Siparis != null && !string.IsNullOrEmpty(k.Siparis!.IdentityUserId))
                .Select(k => k.Siparis!.IdentityUserId!)
                .Distinct()
                .ToList();

            var musteriAdlari = await _context.Users
                .Where(u => musteriIdSet.Contains(u.Id))
                .Select(u => new { u.Id, u.Ad, u.Soyad })
                .ToListAsync();

            ViewBag.Musteriler = musteriAdlari.ToDictionary(x => x.Id, x => $"{x.Ad} {x.Soyad}".Trim());

            return View(kalemler);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Onayla(int id)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            // Satıcının mağazası
            var myStoreId = await _context.Magazalar
                .Where(m => m.OwnerUserId == me.Id)
                .Select(m => m.Id)
                .FirstOrDefaultAsync();

            // Onaylanacak kalem + ilişkiler
            var kalem = await _context.SiparisKalemleri
                .Include(k => k.Urun)
                .Include(k => k.Siparis)
                    .ThenInclude(s => s!.SiparisKalemleri)
                        .ThenInclude(k => k.Urun)
                .FirstOrDefaultAsync(k =>
                    k.Id == id &&
                    k.Urun != null &&
                    k.Urun.StoreId == myStoreId);

            if (kalem == null || kalem.Siparis == null)
            {
                TempData["Error"] = "Kalem/Sipariş bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            // Geçerli sipariş mi?
            if (!CanApprove(kalem.Siparis.Durum))
            {
                TempData["Error"] = "Bu sipariş onaylanamaz.";
                return RedirectToAction(nameof(Index));
            }

            // 1) Kalem onayı
            kalem.KalemDurum = SiparisKalemDurum.SaticiOnayladi;
            _context.SiparisKalemleri.Update(kalem);
            await _context.SaveChangesAsync();

            // 2) Siparişteki TÜM kalemler onaylı mı?
            var s = kalem.Siparis;
            bool tumKalemlerOnayli = s.SiparisKalemleri
                .All(k => k.KalemDurum == SiparisKalemDurum.SaticiOnayladi);

            // Sipariş zaten onaylı ise veya tüm kalemler henüz onaylı değilse burada biteriz.
            if (s.Durum == "Onaylandı" || !tumKalemlerOnayli)
            {
                TempData["Success"] = "Kalem onaylandı.";
                return RedirectToAction(nameof(Index));
            }

            // 3) Tüm kalemler onaylı → siparişi ONAYLA ve tüm etkileri 1 kez uygula
            using var trx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Referans anahtar (gerekiyorsa üret)
                if (string.IsNullOrWhiteSpace(s.OrderRefKey))
                    s.OrderRefKey = $"order-{s.Id}";

                // ---- Stok düş ----
                foreach (var k in s.SiparisKalemleri)
                {
                    if (k.Urun == null) continue;
                    k.Urun.StokAdedi = Math.Max(0, (k.Urun.StokAdedi - k.Miktar));
                    _context.Uruns.Update(k.Urun);
                }

                // ---- Sipariş toplamını garanti altına al ----
                var siparisToplam = s.SiparisKalemleri.Sum(k => k.BirimFiyat * k.Miktar);
                s.ToplamTutar = siparisToplam;

                // 🔑 ÖNEMLİ: BONUS MOTORU ÇALIŞABİLSİN DİYE ÖNCE DURUMU "Onaylandı" YAP
                s.Durum = "Onaylandı";
                s.ReddetmeNedeni = null;
                _context.Siparisler.Update(s);

                // ❗ ToplamHarcama'yı burada artırma — bunu ReferralBonusHandler yapıyor (4000 TL eşiği için gerekli).
                // var user = await _userManager.FindByIdAsync(s.IdentityUserId); ...

                // Idempotent kilit: Sipariş sınıfında HasTriggeredReferral varsa kullan
                bool alreadyTriggered = false;
                var hasTrigProp = typeof(Siparis).GetProperty("HasTriggeredReferral");
                if (hasTrigProp != null)
                {
                    alreadyTriggered = (bool?)hasTrigProp.GetValue(s) == true;
                }

                // Sipariş durumunu ve stok düşümü kesinleştir
                await _context.SaveChangesAsync();

                // ---- Bonuslar (doğrudan/zincir) + aktif referans sayıları ----
                if (!alreadyTriggered)
                {
                    var bonusHandler = new ReferralBonusHandler(_context, _userManager, _config);
                    await bonusHandler.ApplyOrderEffectsAsync(s); // admin'e asla bonus yok kuralı içeride

                    if (hasTrigProp != null)
                    {
                        hasTrigProp.SetValue(s, true);
                        _context.Siparisler.Update(s);
                        await _context.SaveChangesAsync();
                    }
                }

                // ---- Hizmet bedeli & şirket kârı (mevcut mantık korunur) ----
                decimal AdminTop(SiparisKalemi k) => k.AdminFiyatAnlik ?? (k.BirimFiyat * k.Miktar);
                decimal UrunTop(SiparisKalemi k) => k.SaticiTeklifAnlik ?? (k.BirimFiyat * k.Miktar);

                var adminToplam = s.SiparisKalemleri.Sum(AdminTop);
                var hizmetToplam = _config.Value.BonusMiktari; // panelden gelen toplam hizmet bedeli

                foreach (var k in s.SiparisKalemleri)
                {
                    var pay = adminToplam > 0 ? (AdminTop(k) / adminToplam) : 0m;
                    k.HizmetBedeli = Math.Round(hizmetToplam * pay, 2, MidpointRounding.AwayFromZero);
                    k.SirketKari = Math.Round(AdminTop(k) - (UrunTop(k) + k.HizmetBedeli), 2, MidpointRounding.AwayFromZero);
                }

                s.ToplamHizmetBedeli = s.SiparisKalemleri.Sum(x => x.HizmetBedeli);
                s.ToplamSirketKari = s.SiparisKalemleri.Sum(x => x.SirketKari);

                _context.Siparisler.Update(s);
                await _context.SaveChangesAsync();

                await trx.CommitAsync();
                TempData["Success"] = "Sipariş onaylandı.";
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                TempData["Error"] = "Onay sırasında hata: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }


        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Kargola(int id, string kargoTakipNo)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var myStoreId = await _context.Magazalar.Where(m => m.OwnerUserId == me.Id).Select(m => m.Id).FirstOrDefaultAsync();
            var kalem = await _context.SiparisKalemleri
                .Include(k => k.Urun).Include(k => k.Siparis)
                .FirstOrDefaultAsync(k => k.Id == id && k.Urun != null && k.Urun.StoreId == myStoreId);

            if (kalem == null || kalem.Siparis == null)
            {
                TempData["Error"] = "Kalem/Sipariş bulunamadı.";
                return RedirectToAction(nameof(Index));
            }
            if (!CanShip(kalem.Siparis.Durum))
            {
                TempData["Error"] = "Bu sipariş kargolanamaz.";
                return RedirectToAction(nameof(Index));
            }
            if (string.IsNullOrWhiteSpace(kargoTakipNo))
            {
                TempData["Error"] = "Kargo takip numarası zorunlu.";
                return RedirectToAction(nameof(Index));
            }

            kalem.KalemDurum = SiparisKalemDurum.Kargolandı;
            kalem.KargoTakipNo = kargoTakipNo.Trim();
            kalem.Siparis.Durum = "Kargolandı";

            _context.Entry(kalem.Siparis).State = EntityState.Modified;
            _context.SiparisKalemleri.Update(kalem);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Sipariş kargolandı.";
            return RedirectToAction(nameof(Index));
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reddet(int id, string neden)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var myStoreId = await _context.Magazalar
                .Where(m => m.OwnerUserId == me.Id)
                .Select(m => m.Id)
                .FirstOrDefaultAsync();

            var kalem = await _context.SiparisKalemleri
                .Include(k => k.Urun)
                .Include(k => k.Siparis)
                .FirstOrDefaultAsync(k => k.Id == id &&
                                          k.Urun != null &&
                                          k.Urun.StoreId == myStoreId);

            if (kalem == null || kalem.Siparis == null)
            {
                TempData["Error"] = "Kalem/Sipariş bulunamadı.";
                return RedirectToAction(nameof(Index));
            }
            if (kalem.Siparis.Durum != "Beklemede" && kalem.Siparis.Durum != "Onaylandı")
            {
                TempData["Error"] = "Bu sipariş reddedilemez.";
                return RedirectToAction(nameof(Index));
            }
            if (string.IsNullOrWhiteSpace(neden))
            {
                TempData["Error"] = "Reddetme nedeni yazmak zorunludur.";
                return RedirectToAction(nameof(Index));
            }

            // Sipariş ve kalemlerini çek
            var s = await _context.Siparisler
                .Include(x => x.SiparisKalemleri)
                    .ThenInclude(k => k.Urun)
                .FirstOrDefaultAsync(x => x.Id == kalem.Siparis.Id);

            if (s == null)
            {
                TempData["Error"] = "Sipariş bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (string.Equals(s.Durum, "Reddedildi", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Success"] = "Sipariş zaten reddedilmiş.";
                return RedirectToAction(nameof(Index));
            }

            // ⚠️ OID'yi (merchant_oid) siparişten oku: önce OrderRefKey, yoksa IyzicoPaymentId
            string? oid = null;
            var pRef = s.GetType().GetProperty("OrderRefKey");
            if (pRef != null) oid = pRef.GetValue(s)?.ToString();
            if (string.IsNullOrWhiteSpace(oid))
            {
                var pIyz = s.GetType().GetProperty("IyzicoPaymentId");
                if (pIyz != null) oid = pIyz.GetValue(s)?.ToString();
            }
            if (string.IsNullOrWhiteSpace(oid))
            {
                TempData["Error"] = "İade yapılamadı: Bu sipariş için ödeme referansı (OrderRefKey/merchant_oid) bulunmuyor.";
                return RedirectToAction(nameof(Index));
            }

            // İade tutarı: sipariş kalem toplamı
            var toplamTl = Math.Round(s.SiparisKalemleri.Sum(k => k.BirimFiyat * k.Miktar), 2);
            if (toplamTl <= 0)
            {
                TempData["Error"] = "İade tutarı 0 TL olamaz.";
                return RedirectToAction(nameof(Index));
            }

            // PAYTR iade (TL→kuruş çevirir)
            var refund = await _paytr.RefundAsyncTl(
                oid, toplamTl,
                reason: neden,
                description: $"siparisId={s.Id}");

            if (!refund.ok)
            {
                TempData["Error"] = "İade başarısız: " + refund.message;
                return RedirectToAction(nameof(Index));
            }

            using var trx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Onaylıysa stok/harcama/bonus geri al
                if (string.Equals(s.Durum, "Onaylandı", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var k in s.SiparisKalemleri)
                    {
                        if (k.Urun == null) continue;
                        k.Urun.StokAdedi += k.Miktar;
                        _context.Uruns.Update(k.Urun);
                    }

                    var user = await _userManager.FindByIdAsync(s.IdentityUserId);
                    if (user != null)
                    {
                        user.ToplamHarcama -= toplamTl;
                        if (user.ToplamHarcama < 0) user.ToplamHarcama = 0;
                        _context.Users.Update(user);
                    }

                    var bonusHandler = new ReferralBonusHandler(_context, _userManager, _config);
                    await bonusHandler.RevertOrderEffectsAsync(s);
                }

                s.Durum = "Reddedildi";
                s.ReddetmeNedeni = neden.Trim();
                _context.Siparisler.Update(s);
                _context.SiparisKalemleri.Update(kalem);

                await _context.SaveChangesAsync();
                await trx.CommitAsync();

                TempData["Success"] = "İade gerçekleştirildi ve sipariş reddedildi.";
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                TempData["Error"] = "İşlem sırasında hata: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }




        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Tamamla(int id)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var myStoreId = await _context.Magazalar.Where(m => m.OwnerUserId == me.Id).Select(m => m.Id).FirstOrDefaultAsync();
            var kalem = await _context.SiparisKalemleri
                .Include(k => k.Urun).Include(k => k.Siparis)
                .FirstOrDefaultAsync(k => k.Id == id && k.Urun != null && k.Urun.StoreId == myStoreId);

            if (kalem == null || kalem.Siparis == null)
            {
                TempData["Error"] = "Kalem/Sipariş bulunamadı.";
                return RedirectToAction(nameof(Index));
            }
            if (!CanComplete(kalem.Siparis.Durum))
            {
                TempData["Error"] = "Bu sipariş tamamlanamaz.";
                return RedirectToAction(nameof(Index));
            }

            kalem.Siparis.Durum = "Tamamlandı";
            _context.Entry(kalem.Siparis).State = EntityState.Modified;
            _context.SiparisKalemleri.Update(kalem);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Sipariş tamamlandı.";
            return RedirectToAction(nameof(Index));
        }
    }
}
