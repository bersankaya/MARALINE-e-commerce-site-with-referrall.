using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AlisverisSitesiFinal.Models
{
    public class SaticiOdemeKalemi
    {
        public int Id { get; set; }

        public int SaticiOdemeId { get; set; }
        public SaticiOdeme SaticiOdeme { get; set; } = default!;

        // Kaynak sipariş kalemi
        public int SiparisKalemiId { get; set; }

        // Bilgi amaçlı (görsel/rapor)
        public int UrunId { get; set; }

        [Required]
        public string UrunAdi { get; set; } = default!;

        public int Miktar { get; set; }

        // Birimler
        [Precision(18, 2)]
        public decimal BirimFiyat { get; set; }              // müşteriye satış birim

        [Precision(18, 2)]
        public decimal SaticiTeklifBirimFiyat { get; set; }  // satıcı teklif birim (ödenecek)

        // Hesaplar
        [Precision(18, 2)]
        public decimal SatirBrut { get; set; }           // Miktar * BirimFiyat

        [Precision(18, 2)]
        public decimal SatirSaticiyaNet { get; set; }    // Miktar * SaticiTeklifBirimFiyat

        [Precision(18, 2)]
        public decimal SatirPlatformGeliri { get; set; } // SatirBrut - SatirSaticiyaNet
    }
}
