using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Satici,SaticiAday")]
public class SaticiRaporController : Controller
{
    private readonly UygulamaDbContext _ctx;
    private readonly UserManager<Kullanici> _um;
    public SaticiRaporController(UygulamaDbContext ctx, UserManager<Kullanici> um) { _ctx = ctx; _um = um; }

    public async Task<IActionResult> Satislar()
    {
        var me = await _um.GetUserAsync(User);
        if (me == null) return Unauthorized();

        var myStoreId = await _ctx.Magazalar
            .Where(x => x.OwnerUserId == me.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        var kabulDurumlar = new[] { "Beklemede", "Onaylandı", "Kargolandı", "Tamamlandı" };

        var list = await _ctx.SiparisKalemleri
            .Include(x => x.Siparis).ThenInclude(s => s!.Adres)
            .Include(x => x.Urun)
            .Where(x => x.Urun != null
                     && x.Urun.StoreId == myStoreId
                     && x.Siparis != null
                     && kabulDurumlar.Contains(x.Siparis.Durum))
            .OrderByDescending(x => x.Siparis!.SiparisTarihi)
            .ToListAsync();

        var musteriIdSet = list
            .Where(k => k.Siparis != null && !string.IsNullOrEmpty(k.Siparis!.IdentityUserId))
            .Select(k => k.Siparis!.IdentityUserId!)
            .Distinct()
            .ToList();

        var musteriAdlari = await _ctx.Users
            .Where(u => musteriIdSet.Contains(u.Id))
            .Select(u => new { u.Id, u.Ad, u.Soyad })
            .ToListAsync();

        ViewBag.Musteriler = musteriAdlari.ToDictionary(x => x.Id, x => $"{x.Ad} {x.Soyad}".Trim());

        ViewBag.AdetToplam = list.Sum(k => k.Miktar);
        ViewBag.AdetBeklemede = list.Where(k => k.Siparis?.Durum == "Beklemede").Sum(k => k.Miktar);
        ViewBag.AdetOnaylandi = list.Where(k => k.Siparis?.Durum == "Onaylandı").Sum(k => k.Miktar);
        ViewBag.AdetKargolandi = list.Where(k => k.Siparis?.Durum == "Kargolandı").Sum(k => k.Miktar); // << sabit
        ViewBag.AdetTamamlandi = list.Where(k => k.Siparis?.Durum == "Tamamlandı").Sum(k => k.Miktar);

        return View(list);
    }

    public async Task<IActionResult> Kazanc()
    {
        var me = await _um.GetUserAsync(User);
        if (me == null) return Unauthorized();

        // Mağaza Id (satışları filtrelemek için)
        var myStoreId = await _ctx.Magazalar
            .Where(x => x.OwnerUserId == me.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        var tamamlanan = new[] { "Tamamlandı" };

        // Satıcıya ait TAMAMLANAN satış kalemleri
        var satislar = await _ctx.SiparisKalemleri
            .Include(x => x.Siparis).ThenInclude(s => s!.Adres)
            .Include(x => x.Urun)
            .Where(x => x.Urun != null
                     && x.Urun.StoreId == myStoreId
                     && x.Siparis != null
                     && tamamlanan.Contains(x.Siparis.Durum))
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        var toplamCiro = satislar.Sum(x => x.BirimFiyat * x.Miktar);

        var toplamSaticiKazanci = await (
            from k in _ctx.SiparisKalemleri
            join s in _ctx.Siparisler on k.SiparisId equals s.Id
            join u in _ctx.Uruns on k.UrunId equals u.Id
            where u.StoreId == myStoreId && tamamlanan.Contains(s.Durum)
            select (decimal)((u.SaticiTeklifFiyati ?? 0m) * k.Miktar)
        ).SumAsync();

        // ⬇⬇ ÇEKİLEN TOPLAM: SaticiOdemeler tablosundan, bu satıcıya ödenmiş net tutarların toplamı
        // Not: Alan/isimler farklıysa yorumdaki satırlara göre değiştir.
        var odendiDurumlar = new[] { "Odendi", "Ödendi", "Onaylandı" };

        // Eğer SaticiOdemeler tablosunda SaticiId alanı varsa:
        var cekilenToplam = await _ctx.SaticiOdemeler
            .Where(o => o.SaticiId == me.Id && odendiDurumlar.Contains(o.Durum))
            .SumAsync(o => (decimal?)o.NetToplam) ?? 0m;

        // Eğer sende SaticiOdemeler mağaza üzerinden tutuluyorsa (StoreId kolonu varsa) ŞU BLOĞU KULLAN:
        // var cekilenToplam = await _ctx.SaticiOdemeler
        //     .Where(o => o.StoreId == myStoreId && odendiDurumlar.Contains(o.Durum))
        //     .SumAsync(o => (decimal?)o.NetToplam) ?? 0m;

        // (İstersen geçmişi akordeona basmak için kısa liste)
        ViewBag.Cekimler = await _ctx.SaticiOdemeler
            .Where(o => o.SaticiId == me.Id && odendiDurumlar.Contains(o.Durum))
            .OrderByDescending(o => o.OdemeTarihi)
            .Select(o => new { Tarih = o.OdemeTarihi, Tutar = o.NetToplam, Durum = o.Durum })
            .Take(50)
            .ToListAsync();

        ViewBag.ToplamCiro = toplamCiro;
        ViewBag.ToplamSaticiKazanci = toplamSaticiKazanci;
        ViewBag.ToplamCekilen = cekilenToplam; // << Kazanç sayfasındaki “Çekilen Toplam” artık dolu

        return View(satislar); // @model IEnumerable<SiparisKalemi>
    }

}
