using Microsoft.AspNetCore.Identity;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Admin,Satici,SaticiAday")]
    public class SaticiPanelController : Controller
    {
        private readonly UygulamaDbContext _ctx;
        private readonly UserManager<Kullanici> _um;

        public SaticiPanelController(UygulamaDbContext ctx, UserManager<Kullanici> um)
        {
            _ctx = ctx; _um = um;
        }

        [Authorize(Roles = "Satici,Admin")]
        public async Task<IActionResult> Index()
        {
            var me = await _um.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var myStoreId = await _ctx.Magazalar
                .Where(x => x.OwnerUserId == me.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            var kabulDurumlar = new[] { "Beklemede", "Onaylandı", "Kargolandı", "Tamamlandı" };

            // Toplam satış adedi
            var toplamAdet = await _ctx.SiparisKalemleri
                .Include(k => k.Siparis)
                .Include(k => k.Urun)
                .Where(k => k.Urun != null
                         && k.Urun.StoreId == myStoreId
                         && k.Siparis != null
                         && kabulDurumlar.Contains(k.Siparis.Durum))
                .SumAsync(k => (int?)k.Miktar) ?? 0;

            // Toplam ciro (müşteriye satış fiyatı)
            var toplamCiro = await _ctx.SiparisKalemleri
                .Include(k => k.Siparis)
                .Include(k => k.Urun)
                .Where(k => k.Urun != null
                         && k.Urun.StoreId == myStoreId
                         && k.Siparis != null
                         && kabulDurumlar.Contains(k.Siparis.Durum))
                .SumAsync(k => (decimal?)(k.Miktar * k.BirimFiyat)) ?? 0m;

            // Satıcı kazancı (satıcı teklifi baz alınır)
            var toplamSaticiKazanci = await (
                from k in _ctx.SiparisKalemleri
                join s in _ctx.Siparisler on k.SiparisId equals s.Id
                join u in _ctx.Uruns on k.UrunId equals u.Id
                where u.StoreId == myStoreId && kabulDurumlar.Contains(s.Durum)
                select (decimal)((u.SaticiTeklifFiyati ?? 0m) * k.Miktar)
            ).SumAsync();

            ViewBag.ToplamAdet = toplamAdet;
            ViewBag.ToplamCiro = toplamCiro;
            ViewBag.ToplamSaticiKazanci = toplamSaticiKazanci;

            return View();
        }



        [Authorize(Roles = "Satici,Admin")]
        public async Task<IActionResult> Ozet()
        {
            var me = await _um.GetUserAsync(User);
            if (me == null) return Json(new { toplamSiparis = 0, toplamCiro = 0, kazanc = 0, bekleyen = 0 });

            // Bu kullanıcının mağazası
            var myStoreId = await _ctx.Magazalar
                .Where(x => x.OwnerUserId == me.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            // Durum kümeleri
            var tamamlanan = new[] { "Tamamlandı" };
            var bekleyenler = new[] { "Beklemede", "Onaylandı", "Kargolandı" };

            // Toplam sipariş adedi (kalem miktarı)
            var toplamSiparis = await _ctx.SiparisKalemleri
                .Include(k => k.Siparis).Include(k => k.Urun)
                .Where(k => k.Urun != null && k.Urun.StoreId == myStoreId
                         && k.Siparis != null && tamamlanan.Concat(bekleyenler).Contains(k.Siparis.Durum))
                .SumAsync(k => (int?)k.Miktar) ?? 0;

            // Toplam ciro (müşteriye satış fiyatı) — tamamlananlar
            var toplamCiro = await _ctx.SiparisKalemleri
                .Include(k => k.Siparis).Include(k => k.Urun)
                .Where(k => k.Urun != null && k.Urun.StoreId == myStoreId
                         && k.Siparis != null && tamamlanan.Contains(k.Siparis.Durum))
                .SumAsync(k => (decimal?)(k.Miktar * k.BirimFiyat)) ?? 0m;

            // Kazanç (satıcı teklifi üzerinden) — tamamlananlar
            var kazanc = await (
                from k in _ctx.SiparisKalemleri
                join s in _ctx.Siparisler on k.SiparisId equals s.Id
                join u in _ctx.Uruns on k.UrunId equals u.Id
                where u.StoreId == myStoreId && tamamlanan.Contains(s.Durum)
                select (decimal)((u.SaticiTeklifFiyati ?? 0m) * k.Miktar)
            ).SumAsync();

            // (Opsiyonel) Bekleyen ödenek — bekleyen durumlar
            var bekleyen = await (
                from k in _ctx.SiparisKalemleri
                join s in _ctx.Siparisler on k.SiparisId equals s.Id
                join u in _ctx.Uruns on k.UrunId equals u.Id
                where u.StoreId == myStoreId && bekleyenler.Contains(s.Durum)
                select (decimal)((u.SaticiTeklifFiyati ?? 0m) * k.Miktar)
            ).SumAsync();

            return Json(new { toplamSiparis, toplamCiro, kazanc, bekleyen });
        }

    }
}
