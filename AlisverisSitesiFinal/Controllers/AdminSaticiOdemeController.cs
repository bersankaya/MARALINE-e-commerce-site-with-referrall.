// Controllers/AdminSaticiOdemeController.cs
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
    public class AdminSaticiOdemeController : Controller
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;

        public AdminSaticiOdemeController(UygulamaDbContext context, UserManager<Kullanici> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // SATIŞ KALEMLERİNDEN BEKLEYEN ÖDEME ÖZETİ (satıcı bazlı)
        public async Task<IActionResult> Ozet()
        {
            var bekleyen = _context.SiparisKalemleri
                .Include(k => k.Siparis)
                .Include(k => k.Urun)
                .Where(k => !k.SaticiOdemeyeDahilMi &&
                            (k.KalemDurum == SiparisKalemDurum.tamamlandı || k.Siparis!.Durum == "Tamamlandı"));

            var gruplu = await bekleyen
                .GroupBy(k => k.Urun!.UserId)
                .Select(g => new
                {
                    SaticiId = g.Key!,
                    KalemSayisi = g.Count(),
                    BrutToplam = g.Sum(x => x.Miktar * x.BirimFiyat),
                    NetToplam = g.Sum(x => x.Miktar * (x.SaticiTeklifAnlik ?? (x.Urun!.SaticiTeklifFiyati ?? 0m))),
                    IlkStoreId = g.Select(x => x.Urun!.StoreId).FirstOrDefault()
                })
                .ToListAsync();

            var saticiIds = gruplu.Select(x => x.SaticiId).Distinct().ToList();
            var storeIds = gruplu.Select(x => x.IlkStoreId).Where(id => id != null).Cast<int>().Distinct().ToList();

            var saticilar = await _userManager.Users
                .Where(u => saticiIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Ad, u.Soyad, u.IBAN })
                .ToListAsync();

            var magazalar = await _context.Magazalar
                .Where(m => storeIds.Contains(m.Id))
                .Select(m => new { m.Id, IBAN = m.IBAN })
                .ToListAsync();

            var vm = gruplu.Select(x =>
            {
                var s = saticilar.FirstOrDefault(u => u.Id == x.SaticiId);
                var storeIban = magazalar.FirstOrDefault(m => m.Id == x.IlkStoreId)?.IBAN;
                var iban = !string.IsNullOrWhiteSpace(s?.IBAN) ? s!.IBAN : storeIban;

                return new SellerPayoutOzetVM
                {
                    SaticiId = x.SaticiId,
                    SaticiAdSoyad = s != null ? $"{s.Ad} {s.Soyad}" : x.SaticiId,
                    IBAN = iban ?? "",
                    KalemSayisi = x.KalemSayisi,
                    BrutToplam = x.BrutToplam,
                    NetToplam = x.NetToplam
                };
            })
            .OrderBy(v => v.SaticiAdSoyad)
            .ToList();

            // Üstte genel toplamlar (dashboard için)
            ViewBag.ToplamSatici = vm.Count;
            ViewBag.ToplamBrut = vm.Sum(x => x.BrutToplam);
            ViewBag.ToplamNet = vm.Sum(x => x.NetToplam);
            ViewBag.ToplamKalem = vm.Sum(x => x.KalemSayisi);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OdemeYap(string saticiId, string? aciklama)
        {
            if (string.IsNullOrWhiteSpace(saticiId))
            {
                TempData["ErrorMessage"] = "Geçersiz satıcı.";
                return RedirectToAction(nameof(Ozet));
            }

            var satici = await _userManager.FindByIdAsync(saticiId);
            if (satici == null)
            {
                TempData["ErrorMessage"] = "Satıcı bulunamadı.";
                return RedirectToAction(nameof(Ozet));
            }

            var kalemler = await _context.SiparisKalemleri
                .Include(k => k.Siparis)
                .Include(k => k.Urun)
                .Where(k => !k.SaticiOdemeyeDahilMi &&
                            (k.KalemDurum == SiparisKalemDurum.tamamlandı || k.Siparis!.Durum == "Tamamlandı") &&
                            k.Urun!.UserId == saticiId)
                .ToListAsync();

            if (!kalemler.Any())
            {
                TempData["ErrorMessage"] = "Bu satıcı için bekleyen kalem yok.";
                return RedirectToAction(nameof(Ozet));
            }

            // IBAN seçimi: Kullanıcı > Mağaza
            var kullaniciIban = satici.IBAN;
            int? ilkStoreId = kalemler.Select(k => k.Urun!.StoreId).FirstOrDefault();
            string? magazaIban = null;
            if (ilkStoreId.HasValue)
            {
                magazaIban = await _context.Magazalar
                    .Where(m => m.Id == ilkStoreId.Value)
                    .Select(m => m.IBAN)
                    .FirstOrDefaultAsync();
            }
            var kullanilacakIban = !string.IsNullOrWhiteSpace(kullaniciIban) ? kullaniciIban : magazaIban;

            if (string.IsNullOrWhiteSpace(kullanilacakIban))
            {
                TempData["ErrorMessage"] = "IBAN bulunamadı. Satıcı veya mağazaya IBAN ekleyin.";
                return RedirectToAction(nameof(Ozet));
            }

            var odeme = new SaticiOdeme
            {
                SaticiId = saticiId,
                Durum = "Odendi",
                OdemeTarihi = DateTime.UtcNow,
                Aciklama = string.IsNullOrWhiteSpace(aciklama) ? $"Manuel ödeme - {DateTime.UtcNow:yyyy-MM-dd}" : aciklama
            };

            foreach (var k in kalemler)
            {
                var birimSatis = k.BirimFiyat;
                var birimTeklif = k.SaticiTeklifAnlik ?? (k.Urun!.SaticiTeklifFiyati ?? 0m);

                var satirBrut = k.Miktar * birimSatis;
                var satirNet = k.Miktar * birimTeklif;

                odeme.Kalemler.Add(new SaticiOdemeKalemi
                {
                    SiparisKalemiId = k.Id,
                    UrunId = k.UrunId,
                    UrunAdi = k.Urun!.Ad,
                    Miktar = k.Miktar,
                    BirimFiyat = birimSatis,
                    SaticiTeklifBirimFiyat = birimTeklif,
                    SatirBrut = satirBrut,
                    SatirSaticiyaNet = satirNet,
                    SatirPlatformGeliri = satirBrut - satirNet
                });

                k.SaticiOdemeyeDahilMi = true;
            }

            odeme.BrutToplam = odeme.Kalemler.Sum(x => x.SatirBrut);
            odeme.NetToplam = odeme.Kalemler.Sum(x => x.SatirSaticiyaNet);
            odeme.KomisyonToplam = odeme.BrutToplam - odeme.NetToplam;

            _context.SaticiOdemeler.Add(odeme);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Ödeme tamamlandı olarak kaydedildi.";
            return RedirectToAction(nameof(Ozet));
        }
    }
}
