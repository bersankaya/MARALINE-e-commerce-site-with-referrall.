// Models/ViewModels/SellerPayoutOzetVM.cs
namespace AlisverisSitesiFinal.Models.ViewModels
{
    public class SellerPayoutOzetVM
    {
        public string SaticiId { get; set; } = "";
        public string SaticiAdSoyad { get; set; } = "";
        public string? IBAN { get; set; }

        public int KalemSayisi { get; set; }

        // Müşteriye satış toplamı (rapor)
        public decimal BrutToplam { get; set; }

        // Satıcıya ödenecek toplam (SaticiTeklifAnlik)
        public decimal NetToplam { get; set; }

        // Platform geliri = Brut - Net
        public decimal PlatformGeliri => BrutToplam - NetToplam;
    }
}
