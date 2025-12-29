namespace AlisverisSitesiFinal.Models.ViewModels
{
    public class AdminUrunListeVM
    {
        public int UrunId { get; set; }
        public string UrunAdi { get; set; } = "";
        public string? Kategori { get; set; }
        public int Stok { get; set; }
        public DateTime EklenmeTarihi { get; set; }
        public bool YayindaMi { get; set; }

        public string SaticiId { get; set; } = "";
        public string SaticiAdSoyad { get; set; } = "";
        public string SaticiEmail { get; set; } = "";

        public decimal? SaticiTeklifFiyati { get; set; }  // ← satıcının verdiği fiyat
        public decimal? FiyatAdmin { get; set; }          // admin normal fiyat
        public decimal? FiyatReferansli { get; set; }     // referanslı fiyat
    }
}
