using System;
using System.Collections.Generic;

namespace AlisverisSitesiFinal.Models.ViewModels
{
    public class AdminOdemelerDashboardVM
    {
        // Kartlar
        public decimal ToplamOdemelerNet { get; set; }     // SaticiOdeme.NetToplam (Odendi)
        public decimal BekleyenOdemelerNet { get; set; }   // Henüz pakete girmemiş kalemlerden net
        public decimal PlatformKazanci { get; set; }       // Odendi kalemlerinden toplam (Brut - Net)
        public decimal MusteriyeDagitim { get; set; }      // BonusLog toplamı (varsa)

        // Grafik (son 12 ay)
        public List<string> AyEtiketleri { get; set; } = new();
        public List<decimal> AylikSaticiOdemeNet { get; set; } = new();
        public List<decimal> AylikPlatformKazanci { get; set; } = new();
        public List<decimal> AylikMusteriDagitim { get; set; } = new();
        public decimal ToplamBonusDagitim { get; set; }     // BonusLog toplamı
        public decimal ToplamMusteriCekim { get; set; }     // Onaylanan para çekimlerinin toplamı

        public List<decimal> AylikBonusDagitim { get; set; } = new();   // Son 12 ay bonus
        public List<decimal> AylikMusteriCekim { get; set; } = new();   // Son 12 ay çekimler


        // Top satıcılar (son 90 gün)
        public List<TopSellerRow> TopSaticilar { get; set; } = new();

        public class TopSellerRow
        {
            public string SaticiId { get; set; } = "";
            public string SaticiAdSoyad { get; set; } = "";
            public decimal NetToplam { get; set; }
            public decimal PlatformGeliri { get; set; }
        }
    }
}
