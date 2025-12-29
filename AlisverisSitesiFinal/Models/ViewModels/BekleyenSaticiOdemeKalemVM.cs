// Models/ViewModels/BekleyenSaticiOdemeKalemVM.cs
namespace AlisverisSitesiFinal.Models.ViewModels
{
    public class BekleyenSaticiOdemeKalemVM
    {
        public int SiparisKalemiId { get; set; }
        public int UrunId { get; set; }
        public string UrunAdi { get; set; } = "";
        public string SaticiId { get; set; } = "";
        public string SaticiAdSoyad { get; set; } = "";
        public int Miktar { get; set; }

        public decimal BirimFiyat { get; set; }               // müşteriye satış birim
        public decimal SaticiTeklifBirimFiyat { get; set; }   // satıcı teklif birim (ödenecek)

        public decimal SatirBrut => Miktar * BirimFiyat;
        public decimal SatirSaticiyaNet => Miktar * SaticiTeklifBirimFiyat;
        public decimal SatirPlatformGeliri => SatirBrut - SatirSaticiyaNet;
    }
}
