using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminRaporController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;

        public AdminRaporController(UygulamaDbContext context, UserManager<Kullanici> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // /AdminRapor/Odemeler
        public async Task<IActionResult> Odemeler()
        {
            var vm = new AdminOdemelerDashboardVM();

            // 1) Yapılan Toplam Ödemeler (Odendi)
            var odendiQuery = _context.SaticiOdemeler
                .Include(o => o.Kalemler)
                .Where(o => o.Durum == "Odendi");

            vm.ToplamOdemelerNet = await odendiQuery.SumAsync(o => (decimal?)o.NetToplam) ?? 0m;
            vm.PlatformKazanci = await odendiQuery.SumAsync(o => (decimal?)o.KomisyonToplam) ?? 0m;

            // 2) Bekleyen (ödeme paketine girmemiş ve tamamlanmış kalemler)
            var bekleyenKalemQuery = _context.SiparisKalemleri
                .Include(k => k.Siparis)
                .Include(k => k.Urun)
                .Where(k => !k.SaticiOdemeyeDahilMi &&
                            (k.KalemDurum == SiparisKalemDurum.tamamlandı || k.Siparis!.Durum == "Tamamlandı"));

            vm.BekleyenOdemelerNet = await bekleyenKalemQuery
                .SumAsync(k => (decimal?)(k.Miktar * (k.SaticiTeklifAnlik ?? (k.Urun!.SaticiTeklifFiyati ?? 0m)))) ?? 0m;

            // 3) TOPLAMLAR — ayrı ayrı: Bonus dağıtımı (BonusLog pozitifler) ve Müşteri çekimleri (onaylanan talepler)
            decimal toplamBonusDagitim = 0m;
            try
            {
                toplamBonusDagitim = await _context.Set<BonusLog>()
                    .Where(b => b.Tutar > 0)
                    .SumAsync(b => (decimal?)b.Tutar) ?? 0m;
            }
            catch { toplamBonusDagitim = 0m; }

            decimal toplamMusteriCekim = await _context.ParaCekmeTalepleri
                .Where(t => t.OnaylandiMi)
                .SumAsync(t => (decimal?)t.Tutar) ?? 0m;

            vm.MusteriyeDagitim = toplamMusteriCekim; // mevcut alanı çekimler için kullanıyoruz
            ViewBag.ToplamBonusDagitim = toplamBonusDagitim;
            ViewBag.ToplamMusteriCekim = toplamMusteriCekim;

            // 4) Son 12 ay (ay bazında)
            DateTime today = DateTime.Today;
            DateTime startMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);

            var aylikSaticiOdemeler = await odendiQuery
                .Where(o => o.OdemeTarihi != null && o.OdemeTarihi >= startMonth)
                .SelectMany(o => o.Kalemler.Select(k => new {
                    Ay = new DateTime(o.OdemeTarihi!.Value.Year, o.OdemeTarihi!.Value.Month, 1),
                    Net = k.SatirSaticiyaNet,
                    Kom = k.SatirPlatformGeliri
                }))
                .GroupBy(x => x.Ay)
                .Select(g => new { Ay = g.Key, Net = g.Sum(y => y.Net), Kom = g.Sum(y => y.Kom) })
                .ToListAsync();

            // Bonus dağıtımları (aylık) — BonusLog pozitifler
            List<(DateTime Ay, decimal Tutar)> aylikBonus = new();
            try
            {
                aylikBonus = await _context.Set<BonusLog>()
                    .Where(b => b.Tarih >= startMonth && b.Tutar > 0)
                    .GroupBy(b => new DateTime(b.Tarih.Year, b.Tarih.Month, 1))
                    .Select(g => new ValueTuple<DateTime, decimal>(g.Key, g.Sum(x => x.Tutar)))
                    .ToListAsync();
            }
            catch { /* BonusLog olmayabilir */ }

            // Müşteri çekimleri (aylık) — Onaylanan talepler
            var aylikCekimler = await _context.ParaCekmeTalepleri
                .Where(t => t.OnaylandiMi && t.TalepTarihi >= startMonth)
                .GroupBy(t => new DateTime(t.TalepTarihi.Year, t.TalepTarihi.Month, 1))
                .Select(g => new { Ay = g.Key, Tutar = g.Sum(x => x.Tutar) })
                .ToListAsync();

            // View tarafı için 12 aylık diziler
            var aylikBonusSeri = new List<decimal>(capacity: 12);
            var aylikCekimSeri = new List<decimal>(capacity: 12);

            for (int i = 0; i < 12; i++)
            {
                var ay = startMonth.AddMonths(i);
                vm.AyEtiketleri.Add(ay.ToString("MM.yyyy"));

                var row = aylikSaticiOdemeler.FirstOrDefault(x => x.Ay == ay);
                vm.AylikSaticiOdemeNet.Add(row?.Net ?? 0m);
                vm.AylikPlatformKazanci.Add(row?.Kom ?? 0m);

                var cekim = aylikCekimler.FirstOrDefault(x => x.Ay == ay)?.Tutar ?? 0m;
                vm.AylikMusteriDagitim.Add(cekim);

                var bonusAy = aylikBonus.FirstOrDefault(x => x.Ay == ay).Tutar;
                aylikBonusSeri.Add(bonusAy);
                aylikCekimSeri.Add(cekim);
            }

            ViewBag.AylikBonusDagitim = aylikBonusSeri;
            ViewBag.AylikMusteriCekim = aylikCekimSeri;

            // 5) Son **30** günde en çok ödeme yapılan satıcılar
            DateTime from30 = today.AddDays(-30);
            var top = await _context.SaticiOdemeler
                .Include(o => o.Kalemler)
                .Where(o => o.Durum == "Odendi" && o.OdemeTarihi >= from30)
                .GroupBy(o => o.SaticiId)
                .Select(g => new {
                    SaticiId = g.Key,
                    Net = g.Sum(x => x.NetToplam),
                    Kom = g.Sum(x => x.KomisyonToplam)
                })
                .OrderByDescending(x => x.Net)
                .Take(10)
                .ToListAsync();

            var saticiIds = top.Select(x => x.SaticiId!).ToList();
            var saticilar = await _userManager.Users
                .Where(u => saticiIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Ad, u.Soyad })
                .ToListAsync();

            vm.TopSaticilar = top.Select(x =>
            {
                var s = saticilar.FirstOrDefault(u => u.Id == x.SaticiId);
                return new AdminOdemelerDashboardVM.TopSellerRow
                {
                    SaticiId = x.SaticiId!,
                    SaticiAdSoyad = s != null ? $"{s.Ad} {s.Soyad}" : x.SaticiId!,
                    NetToplam = x.Net,
                    PlatformGeliri = x.Kom
                };
            }).ToList();

            // 6) Son **30** günde en çok ödeme yapılan müşteriler (onaylanan çekimler)
            var topCust = await _context.ParaCekmeTalepleri
                .Where(t => t.OnaylandiMi && t.TalepTarihi >= from30)
                .GroupBy(t => t.KullaniciId)
                .Select(g => new { KullaniciId = g.Key, Toplam = g.Sum(x => x.Tutar) })
                .OrderByDescending(x => x.Toplam)
                .Take(10)
                .ToListAsync();

            var custIds = topCust.Select(x => x.KullaniciId!).ToList();
            var customers = await _userManager.Users
                .Where(u => custIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Ad, u.Soyad })
                .ToListAsync();

            // ViewBag ile view'a taşı (VM'i değiştirmiyoruz)
            ViewBag.TopMusteriler = topCust.Select(x =>
            {
                var c = customers.FirstOrDefault(u => u.Id == x.KullaniciId);
                var ad = c != null ? $"{c.Ad} {c.Soyad}" : x.KullaniciId!;
                return new { KullaniciId = x.KullaniciId!, AdSoyad = ad, ToplamCekim = x.Toplam };
            }).ToList();

            return View(vm);
        }
    }
}
